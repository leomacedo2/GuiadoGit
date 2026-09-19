using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Portfolio.Api.Controllers;
using Portfolio.Api.Services;
using Xunit;

namespace Portfolio.Api.Tests;

public sealed class GitHubServiceTests
{
    [Fact]
    public async Task FetchesEveryPageAndMapsNullableFields()
    {
        var paths = new List<string>();
        using var client = Client((request, _) =>
        {
            var path = request.RequestUri!.PathAndQuery;
            paths.Add(path);
            object body = paths.Count switch
            {
                1 => new { login = "leomacedo2", name = (string?)null, bio = (string?)null,
                    avatar_url = "https://avatars.githubusercontent.com/u/1", html_url = "https://github.com/leomacedo2", public_repos = 101 },
                2 => Enumerable.Range(1, 100).Select(Repo).ToArray(),
                _ => new[] { Repo(101) }
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(body) });
        });
        var result = await new GitHubService(client).GetPortfolioAsync("leomacedo2", default);
        Assert.Equal("leomacedo2", result.Username);
        Assert.Null(result.Bio);
        Assert.Equal(101, result.PublicRepositories);
        Assert.Equal(101, result.Repositories.Count);
        Assert.Null(result.Repositories[0].Language);
        Assert.Equal("https://github.com/leomacedo2/repo1", result.Repositories[0].Url);
        Assert.Contains("page=2", paths[2]);
    }

    [Theory]
    [InlineData(404, 404)]
    [InlineData(429, 429)]
    [InlineData(401, 502)]
    [InlineData(403, 502)]
    [InlineData(500, 502)]
    public async Task MapsUpstreamErrorsWithoutExposingResponse(int upstream, int expected)
    {
        using var client = Client((_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)upstream)
            { Content = new StringContent("private upstream details") }));
        var error = await Assert.ThrowsAsync<GitHubApiException>(() => new GitHubService(client).GetPortfolioAsync("test", default));
        Assert.Equal(expected, error.StatusCode);
        Assert.DoesNotContain("private upstream details", error.Message);
    }

    [Fact]
    public async Task DetectsRateLimitOnForbidden()
    {
        using var client = Client((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Forbidden);
            response.Headers.Add("X-RateLimit-Remaining", "0");
            return Task.FromResult(response);
        });
        var error = await Assert.ThrowsAsync<GitHubApiException>(() => new GitHubService(client).GetPortfolioAsync("test", default));
        Assert.Equal(429, error.StatusCode);
    }

    [Fact]
    public async Task HandlesMalformedJson()
    {
        using var client = Client((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("broken json") }));
        var error = await Assert.ThrowsAsync<GitHubApiException>(() => new GitHubService(client).GetPortfolioAsync("test", default));
        Assert.Equal(502, error.StatusCode);
    }

    [Fact]
    public async Task HandlesNetworkFailure()
    {
        using var client = Client((_, _) => throw new HttpRequestException("network"));
        var error = await Assert.ThrowsAsync<GitHubApiException>(() => new GitHubService(client).GetPortfolioAsync("test", default));
        Assert.Equal(502, error.StatusCode);
    }

    [Fact]
    public async Task HandlesTimeout()
    {
        using var client = Client((_, _) => throw new TaskCanceledException());
        var error = await Assert.ThrowsAsync<GitHubApiException>(() => new GitHubService(client).GetPortfolioAsync("test", default));
        Assert.Equal(504, error.StatusCode);
    }

    [Theory]
    [InlineData("@test")]
    [InlineData("-test")]
    [InlineData("test-")]
    [InlineData("test--name")]
    [InlineData("https://github.com/test")]
    public async Task RejectsInvalidUsernameBeforeCallingGitHub(string username)
    {
        using var client = Client((_, _) => throw new InvalidOperationException("Must not call GitHub"));
        var controller = new GitHubController(new GitHubService(client));
        var result = await controller.GetProfile(username, default);
        var problem = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(400, problem.StatusCode);
    }

    private static object Repo(int id) => new { id, name = $"repo{id}", description = (string?)null,
        language = (string?)null, html_url = $"https://github.com/leomacedo2/repo{id}", updated_at = "2026-09-17T12:00:00Z" };

    private static HttpClient Client(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
        => new(new StubHandler(respond)) { BaseAddress = new Uri("https://api.github.com/") };

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => respond(request, cancellationToken);
    }
}
