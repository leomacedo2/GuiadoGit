using System.Net;
using System.Net.Http.Json;
using Portfolio.Api.Services;
using Xunit;

namespace Portfolio.Api.Tests;

public sealed class RecentActivityTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-23T12:00:00Z");
    [Fact]
    public void CountsEachRepositoryTechnologyOnceAndDoesNotInventMissingMonths()
    {
        var result = RecentActivity.Build([new(1, Now, Now, ["React", "React", "Python"]), new(2, Now, Now, ["React"]), new(3, Now.AddMonths(-2), Now, ["Python"])], Now);
        Assert.Equal(12, result.Months.Count);
        Assert.Equal(2, result.Series.Single(s => s.Name == "React").Counts[^1]);
        Assert.Null(result.Series.Single(s => s.Name == "React").Counts[^2]);
        Assert.Equal(1, result.Series.Single(s => s.Name == "Python").Counts[^3]);
    }
    [Fact]
    public void SharedRepositoryUsesOnlyLatestSnapshotAndNeverTwoMonths()
    {
        var result = RecentActivity.Build([new(1, Now.AddMonths(-1), Now.AddDays(-5), ["Python"]), new(1, Now, Now, ["React"])], Now);
        Assert.Equal(1, result.RepositoryCount);
        Assert.Equal("React", Assert.Single(result.Series).Name);
        Assert.Equal(1, result.Series[0].Counts.Sum(n => n ?? 0));
    }
    [Fact]
    public void MissingOldAndFutureDatesAreExcludedAndTechnologyCountIsBounded()
    {
        var result = RecentActivity.Build([new(1, null, Now, ["Unknown"]), new(2, Now.AddMonths(-12), Now, ["Old"]), new(3, Now.AddDays(1), Now, ["Future"]),
            new(4, Now, Now, ["a", "b", "c", "d", "e", "f"])], Now);
        Assert.Equal(1, result.MissingPushDates);
        Assert.Equal(1, result.RepositoriesInPeriod);
        Assert.Equal(5, result.Series.Count);
        Assert.DoesNotContain(result.Series, s => s.Name is "Unknown" or "Old" or "Future");
    }
    [Fact]
    public void UsesUtcMonthBoundaryAndNeverRepositoryUpdatedAt()
    {
        var result = RecentActivity.Build([new(1, DateTimeOffset.Parse("2026-08-31T23:30:00-03:00"), Now, ["React"])], Now);
        Assert.Equal(1, result.Series[0].Counts[^1]);
        Assert.Null(result.Series[0].Counts[^2]);
    }
    [Fact]
    public async Task PushDateComesFromExistingListWithoutCommitRequests()
    {
        var paths = new List<string>();
        using var client = new HttpClient(new Stub(request =>
        {
            paths.Add(request.RequestUri!.AbsolutePath);
            object body = paths.Count == 1 ? new { login = "demo", public_repos = 1, avatar_url = "", html_url = "" }
                : new[] { new { id = 1, name = "repo", html_url = "", updated_at = Now, pushed_at = Now.AddDays(-2) } };
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };
        })) { BaseAddress = new Uri("https://api.github.com/") };
        var result = await new GitHubService(client).GetPortfolioAsync("demo", default);
        Assert.Equal(Now.AddDays(-2), result.Repositories[0].PushedAt);
        Assert.Equal(new[] { "/users/demo", "/users/demo/repos" }, paths);
        Assert.DoesNotContain(paths, p => p.Contains("commits"));
    }
    private sealed class Stub(Func<HttpRequestMessage, HttpResponseMessage> reply) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(reply(request));
    }
}
