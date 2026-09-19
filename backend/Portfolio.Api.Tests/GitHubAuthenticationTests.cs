using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Configuration;
using Portfolio.Api.Services;
using Xunit;

namespace Portfolio.Api.Tests;

public sealed class GitHubAuthenticationTests
{
    private static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.ToDictionary(x => x.Key, x => x.Value)).Build();

    [Fact]
    public void NoCredentialsLeavesClientAnonymous()
    {
        using var client = new HttpClient();
        GitHubClientConfiguration.Configure(client, Config());
        Assert.Null(client.DefaultRequestHeaders.Authorization);
        Assert.Equal("https://api.github.com/", client.BaseAddress!.AbsoluteUri);
        Assert.NotEmpty(client.DefaultRequestHeaders.UserAgent);
    }

    [Fact]
    public void OAuthAppUsesBasicHeaderOnly()
    {
        using var client = new HttpClient();
        GitHubClientConfiguration.Configure(client, Config(("GitHub:ClientId", "fictional-client"), ("GitHub:ClientSecret", "fictional-secret")));
        Assert.Equal("Basic", client.DefaultRequestHeaders.Authorization!.Scheme);
        Assert.Equal("fictional-client:fictional-secret", Encoding.UTF8.GetString(Convert.FromBase64String(client.DefaultRequestHeaders.Authorization.Parameter!)));
        Assert.Equal("https://api.github.com/", client.BaseAddress!.AbsoluteUri);
    }

    [Theory]
    [InlineData("GitHub:ClientId")]
    [InlineData("GitHub:ClientSecret")]
    public void IncompleteCredentialsFailWithoutLeakingValues(string key)
    {
        var error = Assert.Throws<InvalidOperationException>(() => GitHubClientConfiguration.Validate(Config((key, "fictional-value"))));
        Assert.DoesNotContain("fictional-value", error.Message);
    }

    [Fact]
    public void AmbiguousAuthenticationFailsAndLegacyTokenStillWorksAlone()
    {
        Assert.Throws<InvalidOperationException>(() => GitHubClientConfiguration.Validate(Config(("GitHub:ClientId", "fake-id"), ("GitHub:ClientSecret", "fake-secret"), ("GitHub:Token", "fake-token"))));
        using var client = new HttpClient();
        GitHubClientConfiguration.Configure(client, Config(("GitHub:Token", "fake-token")));
        Assert.Equal("Bearer", client.DefaultRequestHeaders.Authorization!.Scheme);
    }

    [Theory]
    [InlineData(403)]
    [InlineData(429)]
    public async Task ExhaustionStopsSubsequentRequestsAndReadsHeaders(int status)
    {
        var state = new GitHubRateLimitState();
        var calls = 0;
        using var client = Client(state, _ => { calls++; return Response(status, 0); });
        var error = await Assert.ThrowsAsync<GitHubApiException>(() => client.GetAsync("https://api.github.com/users/test"));
        Assert.Equal(429, error.StatusCode);
        await Assert.ThrowsAsync<GitHubApiException>(() => client.GetAsync("https://api.github.com/users/test"));
        Assert.Equal(1, calls);
        Assert.Equal(5000, state.Snapshot.Limit);
        Assert.Equal(0, state.Snapshot.Remaining);
        Assert.NotNull(state.Snapshot.ResetAt);
    }

    [Fact]
    public async Task SuccessfulLastRequestIsPreservedBeforeBlockingNext()
    {
        var state = new GitHubRateLimitState();
        using var client = Client(state, _ => Response(200, 0));
        using var response = await client.GetAsync("https://api.github.com/meta");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await Assert.ThrowsAsync<GitHubApiException>(() => client.GetAsync("https://api.github.com/meta"));
    }

    [Fact]
    public async Task RateLimitDuringPaginationPreservesProfileAndFirstPage()
    {
        var state = new GitHubRateLimitState();
        var calls = 0;
        using var client = Client(state, _ =>
        {
            calls++;
            var response = Response(200, calls == 1 ? 1 : 0);
            response.Content = calls == 1
                ? JsonContent.Create(new { login = "test", name = "Test", bio = "", avatar_url = "", html_url = "", public_repos = 101 })
                : JsonContent.Create(Enumerable.Range(1, 100).Select(id => new { id, name = $"repo{id}", language = "Python", html_url = "", updated_at = "2026-01-01T00:00:00Z" }));
            return response;
        });
        var result = await new AnalysisService(new GitHubService(client), new SkillDetector(), new RecommendationService()).AnalyzeAsync("test", default);
        Assert.Equal(2, calls);
        Assert.Equal(100, result.Profile.Repositories.Count);
        Assert.Equal(101, result.Profile.PublicRepositories);
        Assert.True(result.IsPartial);
        Assert.Contains(result.Warnings, w => w.Contains("Lista de repositórios incompleta"));
        Assert.Contains(result.Skills, skill => skill.Name == "Python");
        Assert.Equal(0, result.AdditionalRequests);
    }

    [Fact]
    public void RetryAfterAndMissingHeadersHaveFiniteCooldown()
    {
        var state = new GitHubRateLimitState();
        using var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromMinutes(2));
        Assert.Throws<GitHubApiException>(() => state.Observe(response));
        Assert.Null(state.Snapshot.Limit);
        Assert.True(state.Snapshot.BlockedUntil > DateTimeOffset.UtcNow.AddSeconds(100));
    }

    [Fact]
    public void ExpiredQuotaAllowsNewRequest()
    {
        var state = new GitHubRateLimitState();
        using var response = Response(200, 5000);
        state.Observe(response);
        state.EnsureAvailable();
        Assert.Null(state.Snapshot.BlockedUntil);
    }

    private static HttpResponseMessage Response(int status, int remaining)
    {
        var response = new HttpResponseMessage((HttpStatusCode)status);
        response.Headers.Add("x-ratelimit-limit", "5000");
        response.Headers.Add("x-ratelimit-remaining", remaining.ToString());
        response.Headers.Add("x-ratelimit-reset", DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds().ToString());
        return response;
    }
    private static HttpClient Client(GitHubRateLimitState state, Func<HttpRequestMessage, HttpResponseMessage> respond)
        => new(new GitHubRateLimitHandler(state) { InnerHandler = new Stub(respond) }) { BaseAddress = new Uri("https://api.github.com/") };
    private sealed class Stub(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(respond(request));
    }
}
