using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Portfolio.Api.Auth;
using Portfolio.Api.Data;
using Xunit;

namespace Portfolio.Api.Tests;

public sealed class RefreshSessionTests(PersistenceFixture fixture) : IClassFixture<PersistenceFixture>
{
    private const string Password = "Fictional-Test123!";
    private const string Origin = "http://localhost:5173";
    private HttpClient Client(bool production = false) => fixture.WithWebHostBuilder(builder =>
    {
        if (production) builder.UseEnvironment("Production").UseSetting("Frontend:BaseUrl", "https://frontend.example.test");
    }).CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
    private static string Cookie(HttpResponseMessage response) => response.Headers.GetValues("Set-Cookie")
        .Single(value => value.StartsWith(RefreshSessionService.CookieName + "=")).Split(';')[0];
    private static string Hash(string cookie) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cookie.Split('=', 2)[1])));
    private static async Task<(string Cookie, string Access, string Id)> Login(HttpClient client, string? previous = null)
    {
        var email = $"refresh-{Guid.NewGuid():N}@example.test";
        var registered = await client.PostAsJsonAsync("/api/auth/register", new { Nome = "Session Test", Email = email, Password, ConfirmPassword = Password });
        registered.EnsureSuccessStatusCode();
        var id = (await registered.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login") { Content = JsonContent.Create(new { Email = email, Password }) };
        if (previous is not null) request.Headers.Add("Cookie", previous);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(600, json.GetProperty("expiresIn").GetInt32());
        Assert.False(json.TryGetProperty("refreshToken", out _));
        Assert.DoesNotContain(Cookie(response).Split('=', 2)[1], json.ToString());
        return (Cookie(response), json.GetProperty("accessToken").GetString()!, id);
    }
    private static Task<HttpResponseMessage> Post(HttpClient client, string path, string? cookie = null, string? access = null, string? origin = Origin, bool header = true)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/" + path);
        if (cookie is not null) request.Headers.Add("Cookie", cookie);
        if (access is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        if (origin is not null) request.Headers.Add("Origin", origin);
        if (header) request.Headers.Add("X-Session-Request", "1");
        return client.SendAsync(request);
    }
    private async Task Update(string cookie, string change)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PortfolioDbContext>();
        var session = await db.RefreshSessions.SingleAsync(s => s.TokenHash == Hash(cookie));
        if (change == "expired") session.ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1);
        else if (change == "revoked") session.RevokedAt = DateTimeOffset.UtcNow;
        else session.SecurityStamp = "a-different-stamp";
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task LoginStoresOnlyHashAndOfficialAccessAuthenticatesTheCorrectUser()
    {
        using var client = Client(); var login = await Login(client);
        using var scope = fixture.Services.CreateScope();
        var session = await scope.ServiceProvider.GetRequiredService<PortfolioDbContext>().RefreshSessions.SingleAsync(s => s.ApplicationUserId == login.Id);
        Assert.Equal(Hash(login.Cookie), session.TokenHash);
        Assert.Equal(64, session.TokenHash.Length);
        Assert.InRange((session.ExpiresAt - session.CreatedAt).TotalDays, 29.99, 30.01);
        Assert.Null(session.RevokedAt);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Access);
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.Equal(login.Id, me.GetProperty("id").GetString());
    }

    [Fact]
    public async Task RefreshRotatesAndRejectsReplayWithoutDeletingTheReplacementCookie()
    {
        using var client = Client(); var login = await Login(client);
        var response = await Post(client, "refresh", login.Cookie);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(json.TryGetProperty("refreshToken", out _));
        var replacement = Cookie(response);
        Assert.NotEqual(login.Cookie, replacement);
        var replay = await Post(client, "refresh", login.Cookie);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.False(replay.Headers.Contains("Set-Cookie"));
        Assert.Equal(HttpStatusCode.OK, (await Post(client, "refresh", replacement)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.GetProperty("accessToken").GetString());
        Assert.Equal(login.Id, (await client.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("id").GetString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("guidogit-refresh=invalid")]
    [InlineData("guidogit-refresh=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task MissingOrInvalidRefreshIsUnauthorized(string? cookie)
    {
        using var client = Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(client, "refresh", cookie)).StatusCode);
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("revoked")]
    [InlineData("stamp")]
    public async Task ExpiryRevocationAndIdentityStampAreEnforced(string change)
    {
        using var client = Client(); var login = await Login(client);
        await Update(login.Cookie, change);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(client, "refresh", login.Cookie)).StatusCode);
    }

    [Fact]
    public async Task LogoutRevokesCookieAndCannotRestoreSessionAfterwards()
    {
        using var client = Client(); var login = await Login(client);
        var logout = await Post(client, "logout", login.Cookie, login.Access);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Contains("expires=Thu, 01 Jan 1970", logout.Headers.GetValues("Set-Cookie").Single());
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(client, "refresh", login.Cookie)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Post(client, "logout")).StatusCode);
    }

    [Fact]
    public async Task AnotherAuthenticatedAccountCannotRefreshOrRevokeTheVictimsCookie()
    {
        using var client = Client(); var first = await Login(client); var second = await Login(client);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(client, "refresh", first.Cookie, second.Access)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(client, "logout", first.Cookie, second.Access)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(client, "refresh", first.Cookie, first.Access)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(client, "refresh", second.Cookie, second.Access)).StatusCode);
    }

    [Fact]
    public async Task RotationIsAtomicAndWorksAcrossFreshApplicationInstances()
    {
        using var first = Client(); using var second = Client(); var login = await Login(first);
        var responses = await Task.WhenAll(Post(first, "refresh", login.Cookie), Post(second, "refresh", login.Cookie));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Unauthorized);
        using var restarted = Client();
        Assert.Equal(HttpStatusCode.OK, (await Post(restarted, "refresh", Cookie(responses.Single(r => r.IsSuccessStatusCode)))).StatusCode);
    }

    [Fact]
    public async Task ConcurrentLogoutCannotLeaveTheRotatedSessionActive()
    {
        using var client = Client(); var login = await Login(client);
        var responses = await Task.WhenAll(Post(client, "refresh", login.Cookie), Post(client, "logout", login.Cookie, login.Access));
        Assert.Equal(HttpStatusCode.NoContent, responses[1].StatusCode);
        if (responses[0].IsSuccessStatusCode)
            Assert.Equal(HttpStatusCode.Unauthorized, (await Post(client, "refresh", Cookie(responses[0]))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(client, "refresh", login.Cookie)).StatusCode);
    }

    [Fact]
    public async Task CookieEndpointsRejectCsrfAndCorsAllowsCredentialsOnlyForConfiguredOrigin()
    {
        using var client = Client(); var login = await Login(client);
        foreach (var endpoint in new[] { "refresh", "logout" })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await Post(client, endpoint, login.Cookie, header: false)).StatusCode);
            var denied = await Post(client, endpoint, login.Cookie, origin: "https://untrusted.example.test");
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            Assert.False(denied.Headers.Contains("Access-Control-Allow-Origin"));
        }
        var allowed = await Post(client, "refresh", login.Cookie);
        Assert.Equal(Origin, allowed.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal("true", allowed.Headers.GetValues("Access-Control-Allow-Credentials").Single());
        using var badLogin = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        { Content = JsonContent.Create(new { Email = "test@example.test", Password }) };
        badLogin.Headers.Add("Origin", "https://untrusted.example.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(badLogin)).StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CookieHasPersistentAndEnvironmentAppropriateAttributes(bool production)
    {
        using var client = Client(production); var login = await Login(client);
        var response = await Post(client, "refresh", login.Cookie, origin: production ? "https://frontend.example.test" : Origin);
        response.EnsureSuccessStatusCode();
        var cookie = response.Headers.GetValues("Set-Cookie").Single().ToLowerInvariant();
        Assert.Contains("httponly", cookie); Assert.Contains("path=/api/auth", cookie); Assert.Contains("expires=", cookie);
        Assert.Contains(production ? "samesite=none" : "samesite=lax", cookie);
        Assert.Equal(production, cookie.Contains("; secure"));
        Assert.DoesNotContain("domain=", cookie);
    }
}
