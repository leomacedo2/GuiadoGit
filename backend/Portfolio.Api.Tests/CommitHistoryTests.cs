using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Portfolio.Api.Data;
using Portfolio.Api.DTOs;
using Portfolio.Api.Models;
using Portfolio.Api.Services;
using Xunit;

namespace Portfolio.Api.Tests;

public sealed class CommitHistoryTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-23T12:00:00Z");
    private static RepositoryDto Repo(long id = 1) => new(id, $"repo{id}", null, "JavaScript", "", Now) { PushedAt = Now.AddDays(-id) };
    private static PortfolioDto Profile(params RepositoryDto[] repos) => new("demo", null, null, "", "", repos.Length, repos);
    private static SkillDto Skill(string name, params long[] ids) => new(name, "Frontend", "Boa evidência", ids.Length,
        ids.Select(id => new EvidenceDto(id, $"repo{id}", "", "Manifesto")).ToList());
    private static GitHubCommit Commit(string sha, DateTimeOffset? date = null) => new(sha, new(new(date ?? Now.AddDays(-1))));
    private static Task<CommitActivityDto> Collect(Fake api, IndividualAnalysisOptions? options = null, params RepositoryDto[] repos)
        => new CommitHistoryService(api, options ?? new()).CollectAsync(Profile(repos.Length > 0 ? repos : [Repo()]),
            [Skill("React", 1, 2, 3), Skill("JavaScript", 1, 2, 3)], Now, default, default);

    [Fact]
    public async Task GroupsUtcMonthsDeduplicatesShaAndAssociatesEveryRepositoryTechnology()
    {
        var api = new Fake((_, _) => new([Commit("a"), Commit("a"), Commit("b", Now.AddMonths(-1)),
            Commit("c", DateTimeOffset.Parse("2026-08-31T23:30:00-03:00")), Commit("old", Now.AddYears(-2)), Commit("future", Now.AddDays(1))], false));
        var result = await Collect(api);
        Assert.False(result.IsPartial);
        Assert.Equal("2025-10", result.Months[0]);
        Assert.Equal("2026-09", result.Months[^1]);
        foreach (var series in result.Series)
        {
            Assert.Equal(2, series.Counts[^1]); Assert.Equal(1, series.Counts[^2]); Assert.Equal(0, series.Counts[0]);
        }
        Assert.Equal(3, result.Repositories[0].Commits);
        Assert.Equal(1, result.Requests);
    }

    [Fact]
    public async Task PaginatesAndDeduplicatesAcrossPages()
    {
        var api = new Fake((_, page) => page == 1 ? new([Commit("a")], true) : new([Commit("a"), Commit("b")], false));
        var result = await Collect(api);
        Assert.Equal(new[] { 1, 2 }, api.Pages);
        Assert.Equal(2, result.Series[0].Counts[^1]);
        Assert.Equal(1, result.CompletedRepositories);
    }

    [Fact]
    public async Task PageLimitPreservesCountsAndMarksUnknownInsteadOfZero()
    {
        var result = await Collect(new Fake((_, page) => new([Commit(page.ToString())], true)), new() { MaxCommitPagesPerRepository = 2 });
        Assert.True(result.IsPartial);
        Assert.Equal("page-limit", result.Repositories[0].Status);
        Assert.Equal(2, result.Series[0].Counts[^1]); Assert.Null(result.Series[0].Counts[0]);
        Assert.Equal(2, result.Requests);
    }

    [Fact]
    public async Task RepositorySelectionExcludesForkArchivedInactiveAndNoTechnologyAndPrioritizesRecent()
    {
        var api = new Fake((_, _) => new([], false));
        var repos = new[] { Repo(1), Repo(2) with { PushedAt = Now }, Repo(3), Repo(4) with { IsFork = true },
            Repo(5) with { IsArchived = true }, Repo(6) with { PushedAt = Now.AddYears(-2) }, Repo(7) };
        var skills = new[] { Skill("React", 1, 2, 3, 4, 5, 6) };
        var result = await new CommitHistoryService(api, new() { MaxCommitHistoryRepositories = 2 })
            .CollectAsync(Profile(repos), skills, Now, default, default);
        Assert.Equal(new[] { "repo2", "repo1" }, api.Repositories);
        Assert.Equal(3, result.EligibleRepositories);
        Assert.Equal("repository-limit", result.Repositories[^1].Status);
        Assert.True(result.IsPartial);
    }

    [Fact]
    public async Task RequestBudgetIsSeparateAndStopsBeforeNextRepository()
    {
        var api = new Fake((_, _) => new([Commit("a")], false));
        var result = await Collect(api, new() { CommitHistoryRequestBudget = 1 }, Repo(1), Repo(2));
        Assert.Equal(1, api.RequestCount); Assert.Equal(1, result.CompletedRepositories);
        Assert.Equal("request-budget", result.Repositories[1].Status); Assert.True(result.IsPartial);
    }

    [Theory]
    [InlineData(403)][InlineData(404)][InlineData(502)][InlineData(504)]
    public async Task FailedRepositoryPreservesEarlierPagesAndContinuesOthers(int status)
    {
        var api = new Fake((repo, page) => repo == "repo1" && page == 2 ? throw new GitHubApiException(status, "Simulated")
            : new([Commit(repo)], repo == "repo1"));
        var result = await Collect(api, null, Repo(1), Repo(2));
        Assert.True(result.IsPartial); Assert.Equal(1, result.CompletedRepositories);
        Assert.Equal(2, result.Series[0].Counts[^1]); Assert.Equal(3, result.Requests);
    }

    [Fact]
    public async Task TimeoutContinuesButDeadlineStopsAndCancellationPropagates()
    {
        var api = new Fake((repo, _) => repo == "repo1" ? throw new TaskCanceledException() : new([Commit("a")], false));
        var result = await Collect(api, null, Repo(1), Repo(2));
        Assert.Equal(2, result.Requests); Assert.Equal("timeout", result.Repositories[0].Status);
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        var stopped = await new CommitHistoryService(api, new()).CollectAsync(Profile(Repo()), [Skill("React", 1)], Now, canceled.Token, default);
        Assert.Equal(0, stopped.Requests); Assert.Equal("deadline", stopped.Repositories[0].Status);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new CommitHistoryService(api, new())
            .CollectAsync(Profile(Repo()), [Skill("React", 1)], Now, canceled.Token, canceled.Token));
    }

    [Fact]
    public async Task RateLimitStopsWithoutRetryOrCallingOtherRepositories()
    {
        var api = new Fake((_, _) => throw new GitHubApiException(429, "Limit"));
        var result = await Collect(api, null, Repo(1), Repo(2), Repo(3));
        Assert.Equal(1, result.Requests); Assert.True(result.IsPartial);
        Assert.All(result.Repositories, r => Assert.Equal("rate-limit", r.Status));
    }

    [Fact]
    public async Task ZeroCommitsProducesTwelveRealZeros()
    {
        var result = await Collect(new Fake((_, _) => new([], false)));
        Assert.False(result.IsPartial); Assert.All(result.Series, s => Assert.All(s.Counts, count => Assert.Equal(0, count)));
    }

    [Fact]
    public async Task MalformedEntriesPreserveValidCommitsAndMarkIncomplete()
    {
        var result = await Collect(new Fake((_, _) => new([null!, new("broken", null), Commit("ok")], false)));
        Assert.True(result.IsPartial); Assert.Equal(1, result.Series[0].Counts[^1]);
        Assert.Equal("invalid-data", result.Repositories[0].Status);
    }

    [Fact]
    public async Task SnapshotPersistsOnlyAggregatesAndOldSnapshotHasNoHistory()
    {
        var activity = await Collect(new Fake((_, _) => new([Commit("never-persist-this-sha")], false)));
        var dto = new AnalysisDto(Profile(Repo()), 1, 1, 0, false, [], [Skill("React", 1)], []) { CommitActivity = activity };
        var entity = AnalysisSnapshot.Create(dto, new GitHubProfile { Username = "demo" }, Now, 6);
        Assert.DoesNotContain("never-persist-this-sha", entity.MetadataJson);
        Assert.Contains("commitActivity", entity.MetadataJson);
        Assert.Equal(JsonSerializer.Serialize(activity), JsonSerializer.Serialize(AnalysisSnapshot.Read(entity).CommitActivity));
        entity.MetadataJson = JsonSerializer.Serialize(dto with { CommitActivity = null }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Null(AnalysisSnapshot.Read(entity).CommitActivity);
        Assert.Null(CommitActivityAggregation.ReadMetadata("{}"));
    }

    [Fact]
    public async Task ClassroomSumsStudentsOnceRetainsAllTechnologiesAndDistinguishesMissingHistory()
    {
        var activity = await Collect(new Fake((_, _) => new([Commit("a"), Commit("b")], false)));
        var first = Guid.NewGuid(); var second = Guid.NewGuid();
        var result = CommitActivityAggregation.ForClassroom([(first, activity), (first, activity), (second, activity), (Guid.NewGuid(), null)], Now);
        Assert.Equal(3, result.StudentCount); Assert.Equal(1, result.MissingStudents);
        Assert.Equal(4, result.Series.Single(s => s.Name == "React").Counts[^1]);
        Assert.Null(result.Series[0].Counts[0]); Assert.True(result.IsPartial); Assert.Equal(0, result.Requests);
        var shifted = CommitActivityAggregation.ForClassroom([(first, activity)], Now.AddMonths(1));
        Assert.Null(shifted.Series[0].Counts[^1]); Assert.True(shifted.IsPartial);
    }

    [Fact]
    public async Task SkillsRemainSuccessfulWhenCommitCollectionFailsAndBudgetsAreNotMixed()
    {
        var api = new Fake((_, _) => throw new GitHubApiException(429, "Limit"));
        var result = await new AnalysisService(api, new(), new(), clock: new FixedClock()).AnalyzeAsync("demo", default);
        Assert.False(result.IsPartial); Assert.NotEmpty(result.Skills); Assert.True(result.CommitActivity!.IsPartial);
        Assert.Equal(1, result.AdditionalRequests); // Only the tree; commits have their own counter.
        Assert.Equal(3, result.TotalGitHubRequests); // Profile + tree + failed commit request.
    }

    [Fact]
    public async Task RestEndpointUsesAuthorSinceUntilPaginationAndExistingQuotaHandler()
    {
        var paths = new List<string>();
        var state = new GitHubRateLimitState();
        using var client = new HttpClient(new GitHubRateLimitHandler(state) { InnerHandler = new Stub(request =>
        {
            Assert.Equal("Basic", request.Headers.Authorization!.Scheme);
            paths.Add(request.RequestUri!.AbsolutePath);
            Assert.Equal("/repos/demo/repo1/commits", request.RequestUri.AbsolutePath);
            var query = QueryHelpers.ParseQuery(request.RequestUri.Query);
            Assert.Equal("demo", query["author"]); Assert.Equal("2025-10-01T00:00:00Z", query["since"]);
            Assert.Equal("2026-09-23T12:00:00Z", query["until"]); Assert.Equal("100", query["per_page"]);
            Assert.Equal(paths.Count.ToString(), query["page"]);
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new[] { Commit(paths.Count.ToString()) }) };
            response.Headers.Add("x-ratelimit-limit", "5000"); response.Headers.Add("x-ratelimit-remaining", "4998");
            response.Headers.Add("x-ratelimit-reset", DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds().ToString());
            if (paths.Count == 1) response.Headers.Add("Link", "<https://untrusted.invalid/never-follow>; rel=\"next\"");
            return response;
        }) }) { BaseAddress = new Uri("https://api.github.com/") };
        client.DefaultRequestHeaders.Authorization = new("Basic", "fictitious");
        var api = new GitHubService(client);
        var result = await new CommitHistoryService(api, new()).CollectAsync(Profile(Repo()), [Skill("React", 1)], Now, default, default);
        Assert.Equal(2, result.Requests); Assert.Equal(2, result.Series[0].Counts[^1]); Assert.False(result.IsPartial);
        Assert.Equal(4998, state.Snapshot.Remaining);
    }

    [Fact]
    public async Task ExhaustedHeaderBlocksNextPageBeforeItIsSent()
    {
        var sent = 0;
        using var client = new HttpClient(new GitHubRateLimitHandler(new()) { InnerHandler = new Stub(_ =>
        {
            sent++;
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new[] { Commit("a") }) };
            response.Headers.Add("x-ratelimit-remaining", "0");
            response.Headers.Add("x-ratelimit-reset", DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds().ToString());
            response.Headers.Add("Link", "<https://api.github.com/unused>; rel=\"next\""); return response;
        }) }) { BaseAddress = new Uri("https://api.github.com/") };
        var result = await new CommitHistoryService(new GitHubService(client), new())
            .CollectAsync(Profile(Repo(), Repo(2)), [Skill("React", 1, 2)], Now, default, default);
        Assert.Equal(1, sent); Assert.Equal(1, result.Requests); Assert.True(result.IsPartial);
        Assert.Equal(1, result.Series[0].Counts[^1]);
    }

    private sealed class FixedClock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class Fake(Func<string, int, GitHubCommitPage> reply) : IGitHubService
    {
        public int RequestCount { get; private set; }
        public List<int> Pages { get; } = [];
        public List<string> Repositories { get; } = [];
        public Task<GitHubCommitPage> GetCommitsAsync(string owner, string repository, string author, DateTimeOffset since, DateTimeOffset until, int page, CancellationToken ct)
        { RequestCount++; Pages.Add(page); Repositories.Add(repository); return Task.FromResult(reply(repository, page)); }
        public Task<PortfolioDto> GetPortfolioAsync(string username, CancellationToken ct) { RequestCount++; return Task.FromResult(Profile(Repo())); }
        public Task<GitHubTree> GetTreeAsync(string username, string repository, CancellationToken ct) { RequestCount++; return Task.FromResult(new GitHubTree([], false)); }
        public Task<string> GetBlobAsync(string username, string repository, string sha, CancellationToken ct) => throw new InvalidOperationException("Unexpected blob");
    }
    private sealed class Stub(Func<HttpRequestMessage, HttpResponseMessage> reply) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(reply(request));
    }
}
