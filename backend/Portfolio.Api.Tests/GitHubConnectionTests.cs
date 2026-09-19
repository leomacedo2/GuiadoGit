using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Portfolio.Api.Data;
using Portfolio.Api.DTOs;
using Portfolio.Api.OAuth;
using Portfolio.Api.Services;
using Testcontainers.PostgreSql;
using Xunit;

namespace Portfolio.Api.Tests;

public sealed class OAuthFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private readonly byte[] pfx;
    private int clientId;
    public MockOAuthGitHub GitHub { get; } = new();
    public OAuthFixture()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=Ephemeral-Tests-Only", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        pfx = cert.Export(X509ContentType.Pfx, "test-only-password");
    }
    public async Task InitializeAsync()
    {
        await postgres.StartAsync();
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<PortfolioDbContext>().Database.MigrateAsync();
    }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Portfolio", postgres.GetConnectionString());
        builder.UseSetting("GitHub:ClientId", "fictional-app");
        builder.UseSetting("GitHub:ClientSecret", "fictional-secret");
        builder.UseSetting("GitHub:OAuth:Enabled", "true");
        builder.UseSetting("GitHub:OAuth:CallbackUrl", "https://backend.test/api/github/callback");
        builder.UseSetting("Frontend:BaseUrl", "https://frontend.test");
        builder.UseSetting("DataProtection:CertificateBase64", Convert.ToBase64String(pfx));
        builder.UseSetting("DataProtection:CertificatePassword", "test-only-password");
        builder.ConfigureServices(services =>
        {
            // ALL external HTTP traffic is replaced; unexpected requests fail the test.
            services.AddHttpClient<GitHubOAuthClient>().ConfigurePrimaryHttpMessageHandler(() => new MockOAuthHandler(GitHub));
            services.AddHttpClient<IGitHubService, GitHubService>().ConfigurePrimaryHttpMessageHandler(() => new MockOAuthHandler(GitHub));
            services.AddSingleton<IStartupFilter, TestClientAddress>();
        });
    }
    public HttpClient Client()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://backend.test"), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-Client", "127.0.0." + Interlocked.Increment(ref clientId));
        return client;
    }
    public ServiceProvider FreshKeyProvider()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddDbContext<PortfolioDbContext>(options => options.UseNpgsql(postgres.GetConnectionString()));
        services.AddDataProtection().SetApplicationName("GuiaDoGit").PersistKeysToDbContext<PortfolioDbContext>()
            .ProtectKeysWithCertificate(new X509Certificate2(pfx, "test-only-password", X509KeyStorageFlags.EphemeralKeySet));
        services.AddTransient<GitHubTokenProtection>();
        return services.BuildServiceProvider();
    }
    async Task IAsyncLifetime.DisposeAsync() { await DisposeAsync(); await postgres.DisposeAsync(); }
    private sealed class TestClientAddress : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, continuation) =>
            {
                if (IPAddress.TryParse(context.Request.Headers["X-Test-Client"], out var ip)) context.Connection.RemoteIpAddress = ip;
                return continuation(context);
            });
            next(app);
        };
    }
}

public sealed class MockOAuthGitHub
{
    public ConcurrentDictionary<string, string> Verifiers { get; } = new();
    public ConcurrentDictionary<string, string> Authorizations { get; } = new();
    public ConcurrentDictionary<string, long> Profiles { get; } = new();
    public ConcurrentDictionary<string, bool> RejectedTokens { get; } = new();
    private long nextProfile = 100000;
    public long ProfileId(string name) => Profiles.GetOrAdd(name, _ => Interlocked.Increment(ref nextProfile));
    public int ApiCalls;
}
public sealed class MockOAuthHandler(MockOAuthGitHub fake) : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var path = request.RequestUri!.AbsolutePath;
        if (request.RequestUri.Host == "github.com" && path == "/login/oauth/access_token")
        {
            var form = QueryHelpers.ParseQuery(await request.Content!.ReadAsStringAsync(ct));
            var code = form["code"].ToString();
            fake.Verifiers[code] = form["code_verifier"].ToString();
            if (code == "refused") return Json(new { error = "bad_verification_code" });
            return Json(new { access_token = "mock-token-" + code, scope = code == "broad" ? "repo" : "", token_type = "bearer" });
        }
        Assert.Equal("api.github.com", request.RequestUri.Host);
        var auth = request.Headers.Authorization?.ToString() ?? "none";
        if (path == "/user")
        {
            Assert.StartsWith("Bearer mock-token-", auth);
            var id = long.Parse(auth["Bearer mock-token-".Length..]);
            return Json(new { id, login = "authorized-" + id, avatar_url = "https://example.test/avatar.png" });
        }
        Interlocked.Increment(ref fake.ApiCalls);
        fake.Authorizations[path] = auth;
        if (fake.RejectedTokens.ContainsKey(auth)) return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments is ["users", var name])
            return Json(new { id = fake.ProfileId(name), login = name, name = "Public", avatar_url = "https://example.test/avatar.png", html_url = "https://github.com/" + name, public_repos = 0 });
        if (segments is ["users", _, "repos"]) return Json(Array.Empty<object>());
        throw new InvalidOperationException("Unexpected mocked GitHub path: " + path);
    }
    private static HttpResponseMessage Json(object body)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(body) };
        response.Headers.Add("x-ratelimit-limit", "5000");
        response.Headers.Add("x-ratelimit-remaining", "4321");
        response.Headers.Add("x-ratelimit-reset", DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds().ToString());
        return response;
    }
}

public sealed class GitHubConnectionTests(OAuthFixture fixture) : IClassFixture<OAuthFixture>
{
    private static long identity = 100;
    private static string Unique() => "dev" + Guid.NewGuid().ToString("N")[..16];
    private async Task<(HttpClient Client, string UserId, string Token)> Account()
    {
        var client = fixture.Client();
        var email = Unique() + "@example.test";
        const string password = "Only-Test-Pass123!";
        var register = await client.PostAsJsonAsync("/api/auth/register", new { Nome = "Teste", Email = email, Password = password, ConfirmPassword = password });
        register.EnsureSuccessStatusCode();
        var user = await register.Content.ReadFromJsonAsync<JsonElement>();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = password });
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (client, user.GetProperty("id").GetString()!, token);
    }
    private static async Task<string> Start(HttpClient client)
    {
        var response = await client.PostAsync("/api/github/connect", null);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var uri = response.Headers.Location!;
        Assert.Equal("github.com", uri.Host);
        var query = QueryHelpers.ParseQuery(uri.Query);
        Assert.Equal("", query["scope"].ToString());
        Assert.Equal("S256", query["code_challenge_method"].ToString());
        Assert.Equal("https://backend.test/api/github/callback", query["redirect_uri"].ToString());
        Assert.DoesNotContain("fictional-secret", uri.ToString());
        var cookie = response.Headers.GetValues("Set-Cookie").Single();
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        return query["state"].ToString();
    }
    private static Task<HttpResponseMessage> Callback(HttpClient client, string state, string? code) =>
        client.GetAsync("/api/github/callback?state=" + Uri.EscapeDataString(state) + (code is null ? "" : "&code=" + Uri.EscapeDataString(code)));
    private async Task<long> Connect(HttpClient client)
    {
        var id = Interlocked.Increment(ref identity);
        (await Callback(client, await Start(client), id.ToString())).EnsureSuccessStatusCode();
        return id;
    }
    private static async Task<AnalysisDto> Analyze(HttpClient client, string name, bool force = false)
    {
        var response = force ? await client.PostAsync($"/api/analyses/{name}/refresh", null) : await client.GetAsync($"/api/analyses/{name}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AnalysisDto>())!;
    }

    [Fact]
    public async Task ConnectionEndpointsRequirePlatformAuthentication()
    {
        using var visitor = fixture.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await visitor.PostAsync("/api/github/connect", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await visitor.GetAsync("/api/github/connection")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await visitor.DeleteAsync("/api/github/connection")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await visitor.PostAsync("/api/github/connect?platformToken=invalid", null)).StatusCode);
    }

    [Fact]
    public async Task PopupFormUsesOfficialBearerAndNeverPutsTokenInRedirect()
    {
        var account = await Account(); using var client = account.Client;
        client.DefaultRequestHeaders.Authorization = null;
        var response = await client.PostAsync("/api/github/connect", new FormUrlEncodedContent(new Dictionary<string, string> { ["platformToken"] = account.Token }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.DoesNotContain(account.Token, response.Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/github/connect", new FormUrlEncodedContent(new Dictionary<string, string> { ["platformToken"] = "fake" }))).StatusCode);
    }

    [Fact]
    public async Task StateIsRandomShortLivedHashedAndBoundToBrowserAndAccount()
    {
        var account = await Account(); using var client = account.Client; using var otherBrowser = fixture.Client();
        var first = await Start(client);
        var second = await Start(client);
        Assert.Equal(43, first.Length); Assert.NotEqual(first, second);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PortfolioDbContext>();
        var attempt = await db.GitHubOAuthAttempts.SingleAsync(a => a.ApplicationUserId == account.UserId);
        Assert.DoesNotContain(second, attempt.StateHash);
        Assert.InRange(attempt.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(8), DateTimeOffset.UtcNow.AddMinutes(11));
        Assert.Equal(HttpStatusCode.BadRequest, (await Callback(otherBrowser, second, "101")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Callback(client, first, "101")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Callback(client, new string('x', 43), "101")).StatusCode);
        await db.GitHubOAuthAttempts.Where(a => a.Id == attempt.Id).ExecuteUpdateAsync(u => u.SetProperty(a => a.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)));
        Assert.Equal(HttpStatusCode.BadRequest, (await Callback(client, second, "101")).StatusCode);
    }

    [Fact]
    public async Task CallbackWithoutCodeConsumesAttemptWithoutExchange()
    {
        var account = await Account(); using var client = account.Client;
        var state = await Start(client);
        var count = fixture.GitHub.Verifiers.Count;
        Assert.Equal(HttpStatusCode.BadRequest, (await Callback(client, state, null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Callback(client, state, "101")).StatusCode);
        Assert.Equal(count, fixture.GitHub.Verifiers.Count);
    }

    [Fact]
    public async Task ExchangeUsesPkceAndIdentityFromTokenPersistsOnlyEncryptedSecretForInitiatingAccount()
    {
        var account = await Account(); using var client = account.Client;
        var start = await client.PostAsync("/api/github/connect", null);
        var query = QueryHelpers.ParseQuery(start.Headers.Location!.Query);
        var id = Interlocked.Increment(ref identity);
        // Callback has no platform bearer: ownership comes from the consumed state record.
        client.DefaultRequestHeaders.Authorization = null;
        var callback = await Callback(client, query["state"].ToString(), id.ToString());
        callback.EnsureSuccessStatusCode();
        var html = await callback.Content.ReadAsStringAsync();
        Assert.DoesNotContain("mock-token-", html);
        Assert.Contains("no-store", callback.Headers.CacheControl!.ToString());
        var expected = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(fixture.GitHub.Verifiers[id.ToString()])));
        Assert.Equal(query["code_challenge"].ToString(), expected);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PortfolioDbContext>();
        var connection = await db.GitHubConnections.SingleAsync(c => c.ApplicationUserId == account.UserId);
        Assert.Equal(id, connection.GitHubUserId); Assert.Equal("authorized-" + id, connection.GitHubUsername);
        Assert.DoesNotContain("mock-token-", connection.AccessTokenEncrypted);
        Assert.Equal("mock-token-" + id, scope.ServiceProvider.GetRequiredService<GitHubTokenProtection>().Unprotect(account.UserId, connection.AccessTokenEncrypted));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", account.Token);
        var status = await client.GetStringAsync("/api/github/connection");
        Assert.DoesNotContain("Token", status, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(connection.AccessTokenEncrypted, status);
        Assert.Contains("4321", status);
        Assert.Equal(HttpStatusCode.BadRequest, (await Callback(client, query["state"].ToString(), id.ToString())).StatusCode);
    }

    [Theory]
    [InlineData("refused")]
    [InlineData("broad")]
    public async Task RefusedOrExcessiveScopeGrantsNeverCreateConnection(string code)
    {
        var account = await Account(); using var client = account.Client;
        Assert.Equal(HttpStatusCode.BadRequest, (await Callback(client, await Start(client), code)).StatusCode);
        var status = await client.GetFromJsonAsync<JsonElement>("/api/github/connection");
        Assert.False(status.GetProperty("connected").GetBoolean());
    }

    [Fact]
    public async Task DifferentAccountsHaveIndependentConnectionsButSameGitHubCannotBeLinkedTwice()
    {
        var first = await Account(); using var one = first.Client;
        var second = await Account(); using var two = second.Client;
        var id1 = await Connect(one);
        var state = await Start(two);
        Assert.Equal(HttpStatusCode.BadRequest, (await Callback(two, state, id1.ToString())).StatusCode);
        var id2 = await Connect(two);
        var name1 = Unique(); var name2 = Unique();
        await Analyze(one, name1); await Analyze(two, name2);
        Assert.Equal("Bearer mock-token-" + id1, fixture.GitHub.Authorizations["/users/" + name1]);
        Assert.Equal("Bearer mock-token-" + id2, fixture.GitHub.Authorizations["/users/" + name2]);
        using var scope = fixture.Services.CreateScope();
        var connections = await scope.ServiceProvider.GetRequiredService<PortfolioDbContext>().GitHubConnections.Where(c => c.ApplicationUserId == first.UserId || c.ApplicationUserId == second.UserId).ToListAsync();
        Assert.Equal(2, connections.Count);
    }

    [Fact]
    public async Task FreshSharedCacheSkipsGitHubAndExpiredCacheAndForceRefreshUseUserToken()
    {
        using var visitor = fixture.Client();
        var account = await Account(); using var client = account.Client;
        var name = Unique(); var original = await Analyze(visitor, name);
        Assert.StartsWith("Basic ", fixture.GitHub.Authorizations["/users/" + name]);
        var id = await Connect(client);
        var calls = fixture.GitHub.ApiCalls;
        var cached = await Analyze(client, name);
        Assert.Equal("cache", cached.Source); Assert.Equal(original.Id, cached.Id);
        Assert.Equal(calls, fixture.GitHub.ApiCalls); Assert.Equal(0, cached.CurrentGitHubRequests);
        using (var scope = fixture.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<PortfolioDbContext>().Analyses.Where(a => a.Id == original.Id)
                .ExecuteUpdateAsync(u => u.SetProperty(a => a.ExpiresAt, DateTimeOffset.UtcNow.AddHours(-1)));
        var refreshed = await Analyze(client, name);
        Assert.Equal("github", refreshed.Source);
        Assert.Equal("Bearer mock-token-" + id, fixture.GitHub.Authorizations["/users/" + name]);
        var forced = await Analyze(client, name, true);
        Assert.NotEqual(refreshed.Id, forced.Id);
        Assert.Equal(2, forced.CurrentGitHubRequests);
    }

    [Fact]
    public async Task UnconnectedUserUsesApplicationAuthenticationAndDisconnectPreservesSavedAnalyses()
    {
        var account = await Account(); using var client = account.Client;
        var name = Unique(); var analysis = await Analyze(client, name);
        Assert.StartsWith("Basic ", fixture.GitHub.Authorizations["/users/" + name]);
        (await client.PutAsync($"/api/me/profiles/{analysis.Id}", null)).EnsureSuccessStatusCode();
        await Connect(client);
        (await client.DeleteAsync("/api/github/connection")).EnsureSuccessStatusCode();
        var status = await client.GetFromJsonAsync<JsonElement>("/api/github/connection");
        Assert.False(status.GetProperty("connected").GetBoolean());
        Assert.Single((await client.GetFromJsonAsync<JsonElement>("/api/me/profiles")).EnumerateArray());
        var cached = await Analyze(client, name);
        Assert.Equal(analysis.Id, cached.Id);
        (await client.GetAsync("/api/auth/me")).EnsureSuccessStatusCode();
        await Analyze(client, name, true);
        Assert.StartsWith("Basic ", fixture.GitHub.Authorizations["/users/" + name]);
    }

    [Fact]
    public async Task RevokedTokenIsInvalidatedWithoutRetryAndNextRequestFallsBackToApplication()
    {
        var account = await Account(); using var client = account.Client;
        var id = await Connect(client); var name = Unique();
        fixture.GitHub.RejectedTokens["Bearer mock-token-" + id] = true;
        var before = fixture.GitHub.ApiCalls;
        Assert.Equal(HttpStatusCode.BadGateway, (await client.GetAsync("/api/analyses/" + name)).StatusCode);
        Assert.Equal(before + 1, fixture.GitHub.ApiCalls);
        Assert.True((await client.GetFromJsonAsync<JsonElement>("/api/github/connection")).GetProperty("requiresReconnect").GetBoolean());
        await Analyze(client, name);
        Assert.StartsWith("Basic ", fixture.GitHub.Authorizations["/users/" + name]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExpiredOrUnreadableConnectionFallsBackWithoutSendingItsToken(bool expired)
    {
        var account = await Account(); using var client = account.Client;
        await Connect(client);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PortfolioDbContext>();
            var query = db.GitHubConnections.Where(c => c.ApplicationUserId == account.UserId);
            if (expired) await query.ExecuteUpdateAsync(u => u.SetProperty(c => c.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)));
            else await query.ExecuteUpdateAsync(u => u.SetProperty(c => c.AccessTokenEncrypted, "unreadable-test-ciphertext"));
        }
        var name = Unique(); await Analyze(client, name);
        Assert.StartsWith("Basic ", fixture.GitHub.Authorizations["/users/" + name]);
        Assert.True((await client.GetFromJsonAsync<JsonElement>("/api/github/connection")).GetProperty("requiresReconnect").GetBoolean());
    }

    [Fact]
    public async Task PersistedKeyRingDecryptsAfterProviderRestartAndIsEncryptedAtRest()
    {
        var account = await Account(); using var client = account.Client;
        var id = await Connect(client);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PortfolioDbContext>();
        var encrypted = (await db.GitHubConnections.SingleAsync(c => c.ApplicationUserId == account.UserId)).AccessTokenEncrypted;
        var keys = await db.DataProtectionKeys.ToListAsync();
        Assert.NotEmpty(keys);
        Assert.All(keys, key => { Assert.Contains("encryptedSecret", key.Xml!); Assert.DoesNotContain("<masterKey", key.Xml!); });
        using var restarted = fixture.FreshKeyProvider();
        var protection = restarted.GetRequiredService<GitHubTokenProtection>();
        Assert.Equal("mock-token-" + id, protection.Unprotect(account.UserId, encrypted));
        Assert.Throws<CryptographicException>(() => protection.Unprotect("different-user", encrypted));
    }

    [Fact]
    public async Task ExhaustedUserQuotaDoesNotBlockOtherUserOrApplication()
    {
        var first = await Account(); using var one = first.Client;
        var second = await Account(); using var two = second.Client;
        var id1 = await Connect(one); await Connect(two);
        var quotas = fixture.Services.GetRequiredService<UserGitHubQuotas>();
        using var exhausted = new HttpResponseMessage(HttpStatusCode.OK);
        exhausted.Headers.Add("x-ratelimit-remaining", "0");
        exhausted.Headers.Add("x-ratelimit-reset", DateTimeOffset.UtcNow.AddMinutes(30).ToUnixTimeSeconds().ToString());
        quotas.For(id1).Observe(exhausted);
        var count = fixture.GitHub.ApiCalls;
        Assert.Equal(HttpStatusCode.TooManyRequests, (await one.GetAsync("/api/analyses/" + Unique())).StatusCode);
        Assert.Equal(count, fixture.GitHub.ApiCalls);
        await Analyze(two, Unique());
        using var visitor = fixture.Client(); await Analyze(visitor, Unique());
        Assert.NotEqual(0L, fixture.Services.GetRequiredService<GitHubRateLimitState>().Snapshot.Remaining);
    }
}
