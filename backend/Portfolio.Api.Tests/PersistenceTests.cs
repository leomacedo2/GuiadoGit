using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Portfolio.Api.Data;
using Portfolio.Api.DTOs;
using Portfolio.Api.Models;
using Portfolio.Api.Services;
using Testcontainers.PostgreSql;
using Xunit;

namespace Portfolio.Api.Tests;

// Real PostgreSQL and real HTTP/Identity pipeline; only GitHub is replaced.
public sealed class PersistenceFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    public FakePersistentGitHub GitHub { get; } = new();
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
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IGitHubService>();
            services.AddSingleton<IGitHubService>(GitHub);
        });
    }
    async Task IAsyncLifetime.DisposeAsync() { await DisposeAsync(); await postgres.DisposeAsync(); }
}

public sealed class FakePersistentGitHub : IGitHubService
{
    private int count;
    private long nextId;
    public int RequestCount => count;
    public ConcurrentDictionary<string, int> Calls { get; } = new();
    public ConcurrentDictionary<string, long> Ids { get; } = new();
    public ConcurrentDictionary<string, bool> Blocked { get; } = new();
    public async Task<PortfolioDto> GetPortfolioAsync(string username, CancellationToken ct)
    {
        username = username.ToLowerInvariant();
        Calls.AddOrUpdate(username, 1, (_, previous) => previous + 1);
        Interlocked.Increment(ref count);
        if (Blocked.ContainsKey(username)) throw new GitHubApiException(429, "Limite atingido.");
        await Task.Delay(80, ct);
        var id = Ids.GetOrAdd(username, _ => Interlocked.Increment(ref nextId));
        return new(username, "Perfil de teste", "Bio de teste", "https://example.com/avatar.png",
            $"https://github.com/{username}", 1,
            [new(id, "demo", "Descrição completa", "Python", $"https://github.com/{username}/demo", DateTimeOffset.Parse("2026-09-17T12:00:00Z"))]) { GitHubId = id };
    }
    public Task<GitHubTree> GetTreeAsync(string username, string repository, CancellationToken ct)
    {
        Interlocked.Increment(ref count);
        return Task.FromResult(new GitHubTree([new("main.py", "blob", "file", 10), new("requirements.txt", "blob", "req", 12)], username.StartsWith("partial")));
    }
    public Task<string> GetBlobAsync(string username, string repository, string sha, CancellationToken ct)
    { Interlocked.Increment(ref count); return Task.FromResult("fastapi==0.100.0\nsqlalchemy==2.0.0"); }
}

public sealed class PersistenceTests(PersistenceFixture fixture) : IClassFixture<PersistenceFixture>
{
    [Fact]
    public async Task DatabaseStatusConfirmsAppliedMigrationsWithoutCallingGitHub()
    {
        using var client = fixture.CreateClient();
        var calls = fixture.GitHub.RequestCount;
        var status = await client.GetFromJsonAsync<Portfolio.Api.Deployment.DatabaseStatus>("/api/database/status");
        Assert.Equal(new Portfolio.Api.Deployment.DatabaseStatus(true, true), status);
        Assert.Equal(calls, fixture.GitHub.RequestCount);
    }
    private const string Password = "Only-Test-Pass123!";
    private static string Unique(string prefix = "dev") => prefix + Guid.NewGuid().ToString("N")[..16];
    private async Task<AnalysisDto> Analyze(HttpClient client, string name, bool force = false)
    {
        var response = force ? await client.PostAsync($"/api/analyses/{name}/refresh", null) : await client.GetAsync($"/api/analyses/{name}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AnalysisDto>())!;
    }
    private async Task<HttpClient> Account()
    {
        var client = fixture.CreateClient();
        var email = Unique() + "@example.test";
        (await client.PostAsJsonAsync("/api/auth/register", new { Nome = "Teste", Email = email, Password, ConfirmPassword = Password })).EnsureSuccessStatusCode();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password });
        login.EnsureSuccessStatusCode();
        var json = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.GetProperty("accessToken").GetString());
        return client;
    }
    private async Task Expire(Guid id)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PortfolioDbContext>();
        await db.Analyses.Where(a => a.Id == id).ExecuteUpdateAsync(update => update.SetProperty(a => a.ExpiresAt, DateTimeOffset.UtcNow.AddHours(-1)));
    }

    [Fact]
    public async Task RegisterPersistsIdentityHashAndLoginReturnsOfficialToken()
    {
        using var client = await Account();
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PortfolioDbContext>();
        var user = await db.Users.SingleAsync(u => u.Id == me.GetProperty("id").GetString());
        Assert.Equal("Teste", user.Nome);
        Assert.NotNull(user.PasswordHash);
        Assert.DoesNotContain(Password, user.PasswordHash);
        Assert.True(user.DataCadastro > DateTimeOffset.UtcNow.AddMinutes(-2));
    }

    [Fact]
    public async Task InvalidLoginAndAnonymousPrivateEndpointsAreRejected()
    {
        using var client = fixture.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { Email = "missing@example.test", Password = "wrong" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me/profiles")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsync($"/api/me/profiles/{Guid.NewGuid()}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.DeleteAsync($"/api/me/profiles/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task ConfirmationAndPasswordPolicyAreValidated()
    {
        using var client = fixture.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/register", new { Nome = "Teste", Email = "sample@example.test", Password, ConfirmPassword = "different" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/register", new { Nome = "Teste", Email = "sample@example.test", Password = "weak", ConfirmPassword = "weak" })).StatusCode);
    }

    [Fact]
    public async Task VisitorUsesSharedCacheWithNormalizedUsernameAndNoGitHubCalls()
    {
        using var client = fixture.CreateClient();
        var name = Unique();
        var original = await Analyze(client, name);
        var cached = await Analyze(client, name.ToUpperInvariant());
        Assert.Equal("github", original.Source);
        Assert.Equal("cache", cached.Source);
        Assert.Equal(original.Id, cached.Id);
        Assert.Equal(0, cached.CurrentGitHubRequests);
        Assert.Equal(1, fixture.GitHub.Calls[name]);
        Assert.Equal(TimeSpan.FromHours(6), original.ExpiresAt - original.AnalyzedAt);
    }

    [Fact]
    public async Task ExpiredCacheAndForceRefreshCreateNewSnapshots()
    {
        using var client = fixture.CreateClient();
        var name = Unique();
        var original = await Analyze(client, name);
        var forced = await Analyze(client, name, true);
        Assert.NotEqual(original.Id, forced.Id);
        await Expire(forced.Id!.Value);
        var renewed = await Analyze(client, name);
        Assert.Equal("github", renewed.Source);
        Assert.NotEqual(forced.Id, renewed.Id);
        Assert.Equal(3, fixture.GitHub.Calls[name]);
    }

    [Fact]
    public async Task SnapshotReconstructsEveryCollectionAndWarning()
    {
        using var client = fixture.CreateClient();
        var name = Unique("partial");
        var original = await Analyze(client, name);
        var cached = await Analyze(client, name);
        Assert.True(cached.IsPartial);
        Assert.NotEmpty(cached.Warnings);
        Assert.NotEmpty(cached.Skills);
        Assert.NotEmpty(cached.Recommendations);
        Assert.All(cached.Skills, s => Assert.NotEmpty(s.Evidence));
        // jsonb changes property ordering, not the complete DTO content or collection order.
        Assert.Equal(JsonSerializer.Serialize(original with { Source = "cache", CurrentGitHubRequests = 0 }), JsonSerializer.Serialize(cached));
    }

    [Fact]
    public async Task TwoUsersShareProfileButOwnIndependentSavedLists()
    {
        using var first = await Account();
        using var second = await Account();
        var analysis = await Analyze(first, Unique());
        var other = await Analyze(first, Unique());
        (await first.PutAsync($"/api/me/profiles/{analysis.Id}", null)).EnsureSuccessStatusCode();
        (await first.PutAsync($"/api/me/profiles/{analysis.Id}", null)).EnsureSuccessStatusCode();
        (await first.PutAsync($"/api/me/profiles/{other.Id}", null)).EnsureSuccessStatusCode();
        (await second.PutAsync($"/api/me/profiles/{analysis.Id}", null)).EnsureSuccessStatusCode();
        var list1 = await first.GetFromJsonAsync<JsonElement>("/api/me/profiles");
        var list2 = await second.GetFromJsonAsync<JsonElement>("/api/me/profiles");
        Assert.Equal(2, list1.GetArrayLength());
        Assert.Equal(1, list2.GetArrayLength());
        var sharedId = list2[0].GetProperty("gitHubProfileId").GetGuid();
        var privateId = list1.EnumerateArray().Single(p => p.GetProperty("gitHubProfileId").GetGuid() != sharedId).GetProperty("gitHubProfileId").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await second.GetAsync($"/api/me/profiles/{privateId}/analysis")).StatusCode);
        (await second.DeleteAsync($"/api/me/profiles/{privateId}")).EnsureSuccessStatusCode();
        Assert.Equal(2, (await first.GetFromJsonAsync<JsonElement>("/api/me/profiles")).GetArrayLength());
        (await first.DeleteAsync($"/api/me/profiles/{sharedId}")).EnsureSuccessStatusCode();
        Assert.Equal(1, (await first.GetFromJsonAsync<JsonElement>("/api/me/profiles")).GetArrayLength());
        (await second.GetAsync($"/api/me/profiles/{sharedId}/analysis")).EnsureSuccessStatusCode();
        Assert.Equal("cache", (await Analyze(first, analysis.Profile.Username)).Source);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PortfolioDbContext>();
        Assert.Equal(1, await db.GitHubProfiles.CountAsync(p => p.Id == sharedId));
    }

    [Fact]
    public async Task OpeningExpiredSavedAnalysisNeverCallsGitHub()
    {
        using var client = await Account();
        var name = Unique();
        var snapshot = await Analyze(client, name);
        (await client.PutAsync($"/api/me/profiles/{snapshot.Id}", null)).EnsureSuccessStatusCode();
        await Expire(snapshot.Id!.Value);
        var list = await client.GetFromJsonAsync<JsonElement>("/api/me/profiles");
        Assert.True(list[0].GetProperty("isExpired").GetBoolean());
        var id = list[0].GetProperty("gitHubProfileId").GetGuid();
        var opened = await client.GetFromJsonAsync<AnalysisDto>($"/api/me/profiles/{id}/analysis");
        Assert.Equal(snapshot.Id, opened!.Id);
        Assert.Equal(1, fixture.GitHub.Calls[name]);
    }

    [Fact]
    public async Task RateLimitFailureRetainsPreviousSnapshotWithExplanation()
    {
        using var client = fixture.CreateClient();
        var name = Unique();
        var original = await Analyze(client, name);
        fixture.GitHub.Blocked[name] = true;
        var fallback = await Analyze(client, name, true);
        Assert.Equal(original.Id, fallback.Id);
        Assert.Equal("cache", fallback.Source);
        Assert.Contains("Limite", fallback.PersistenceWarning);
        Assert.Equal(2, fixture.GitHub.Calls[name]);
        Assert.Equal(1, fallback.CurrentGitHubRequests);
    }

    [Fact]
    public async Task ConcurrentRequestsForSameUsernameCollectOnlyOnce()
    {
        using var client = fixture.CreateClient();
        var name = Unique();
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Analyze(client, name)));
        Assert.Single(results.Select(r => r.Id).Distinct());
        Assert.Equal(1, fixture.GitHub.Calls[name]);
    }

    [Fact]
    public async Task ConcurrentRefreshesReuseTheJustCompletedSnapshot()
    {
        using var client = fixture.CreateClient();
        var name = Unique();
        await Analyze(client, name);
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Analyze(client, name, true)));
        Assert.Single(results.Select(r => r.Id).Distinct());
        Assert.Equal(2, fixture.GitHub.Calls[name]);
    }

    [Fact]
    public async Task WrongPasswordForExistingAccountFails()
    {
        using var client = await Account();
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        using var visitor = fixture.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await visitor.PostAsJsonAsync("/api/auth/login", new
            { Email = me.GetProperty("email").GetString(), Password = "Wrong-Test123!" })).StatusCode);
    }
}
