using Portfolio.Api.DTOs;
using Portfolio.Api.Models;
using Portfolio.Api.Services;
using Xunit;
using Microsoft.Extensions.Options;

namespace Portfolio.Api.Tests;

public sealed class AnalysisTests
{
    private static RepositoryDto Repo(int id = 1) => new(id, $"repo{id}", null, "Python", $"https://github.com/test/repo{id}", DateTimeOffset.UtcNow.AddDays(-id));

    [Fact]
    public void DetectsPathsAndRealDependenciesButNotDescriptionOrScripts()
    {
        var inspection = new RepositoryInspection(
            ["src/Main.java", "db/schema.sql", "Dockerfile", ".github/workflows/ci.yml", "tests/test_main.py", "node_modules/ignored/file.ts", "app/package.json", "pom.xml"],
            new Dictionary<string, string> {
                ["app/package.json"] = """{"description":"React Angular", "scripts":{"test":"jest"},"dependencies":{"react":"1","expo":"1"}}""",
                ["pom.xml"] = """<project xmlns="http://maven.apache.org/POM/4.0.0"><dependencies><dependency><groupId>org.springframework.boot</groupId><artifactId>spring-boot-starter-web</artifactId></dependency><dependency><groupId>org.junit.jupiter</groupId><artifactId>junit-jupiter</artifactId></dependency></dependencies></project>"""
            }, false);
        var hits = new SkillDetector().Detect(Repo(), inspection);
        foreach (var name in new[] { "Python", "Java", "SQL", "Maven", "Docker", "CI/CD", "React", "Expo", "React Native", "Spring Boot", "REST API", "JUnit", "Testes automatizados" })
            Assert.Contains(hits, h => h.Name == name);
        Assert.DoesNotContain(hits, h => h.Name is "Angular" or "TypeScript");
        Assert.All(hits, hit => Assert.False(string.IsNullOrWhiteSpace(hit.Reason)));
    }

    [Fact]
    public void IgnoresDependencyManagementAndNamesInComments()
    {
        var hits = new SkillDetector().Detect(Repo(), new([], new Dictionary<string, string> {
            ["pom.xml"] = """<project><!-- spring-boot-starter-web --><dependencyManagement><dependencies><dependency><groupId>org.springframework.boot</groupId><artifactId>spring-boot-starter-web</artifactId></dependency></dependencies></dependencyManagement></project>"""
        }, false));
        Assert.DoesNotContain(hits, h => h.Name is "Spring Boot" or "REST API");
    }

    [Theory]
    [InlineData(1, "Pouca evidência")]
    [InlineData(2, "Em desenvolvimento")]
    [InlineData(3, "Boa evidência")]
    [InlineData(5, "Forte evidência")]
    public async Task LevelsCountRepositoriesNotFiles(int count, string expected)
    {
        var api = new FakeGitHub(count);
        var result = await Service(api).AnalyzeAsync("test", default);
        var python = Assert.Single(result.Skills, s => s.Name == "Python");
        Assert.Equal(count, python.RepositoryCount);
        Assert.Equal(expected, python.EvidenceLevel);
    }

    [Fact]
    public async Task StopsAtBudgetAndPreservesAllRepositories()
    {
        var api = new FakeGitHub(25) { WithManifests = true };
        var result = await new AnalysisService(api, new SkillDetector(), new RecommendationService(),
            Options.Create(new IndividualAnalysisOptions { MaxRequests = 30 })).AnalyzeAsync("test", default);
        Assert.Equal(30, result.AdditionalRequests);
        Assert.Equal(30, api.Calls);
        Assert.Equal(25, result.AnalyzedRepositories);
        Assert.Equal(25, result.Profile.Repositories.Count);
        Assert.True(result.IsPartial);
        Assert.Equal(10, result.InspectedRepositories);
    }

    [Fact]
    public async Task RateLimitDuringManifestKeepsTreeEvidence()
    {
        var api = new FakeGitHub(2) { WithManifests = true, FailBlob = true };
        var result = await Service(api).AnalyzeAsync("test", default);
        Assert.True(result.IsPartial);
        Assert.Equal(2, api.Calls);
        Assert.Contains(result.Skills, s => s.Name == "Docker");
        Assert.Contains(result.Skills, s => s.Name == "Python" && s.RepositoryCount == 2);
    }

    [Fact]
    public async Task EmptyProfileHasNoInventedRecommendations()
    {
        var result = await Service(new FakeGitHub(0)).AnalyzeAsync("test", default);
        Assert.Empty(result.Skills);
        Assert.Empty(result.Recommendations);
        Assert.False(result.IsPartial);
    }

    [Fact]
    public void RecommendationsExplainContextAndDisappearWithMoreEvidence()
    {
        SkillDto Skill(string name, int count = 1) => new(name, "Test", "Pouca evidência", count, []);
        var service = new RecommendationService();
        var result = service.Recommend([Skill("Java"), Skill("Maven"), Skill("Spring Boot"), Skill("REST API")]);
        Assert.Equal(new[] { "JPA", "JUnit", "Docker" }, result.Select(r => r.Topic));
        Assert.All(result, r => { Assert.NotEmpty(r.Reason); Assert.NotEmpty(r.NextStep); });
        Assert.Empty(service.Recommend([Skill("Java"), Skill("Maven"), Skill("Spring Boot"), Skill("REST API"), Skill("JPA"), Skill("SQL"), Skill("JUnit", 2), Skill("Docker", 2)]));
    }

    [Fact]
    public async Task InvalidManifestDoesNotDiscardPaths()
    {
        var api = new FakeGitHub(1) { WithManifests = true, InvalidBlob = true };
        var result = await Service(api).AnalyzeAsync("test", default);
        Assert.True(result.IsPartial);
        Assert.Contains(result.Skills, s => s.Name == "Docker");
    }

    [Fact]
    public async Task CancellationPropagates()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service(new FakeGitHub(1)).AnalyzeAsync("test", cancellation.Token));
    }

    private static AnalysisService Service(FakeGitHub api) => new(api, new SkillDetector(), new RecommendationService());

    private sealed class FakeGitHub(int count) : IGitHubService
    {
        public int Calls { get; private set; }
        public bool WithManifests { get; init; }
        public bool FailBlob { get; init; }
        public bool InvalidBlob { get; init; }
        public Task<PortfolioDto> GetPortfolioAsync(string username, CancellationToken cancellationToken)
            => Task.FromResult(new PortfolioDto("test", null, null, "", "", count, Enumerable.Range(1, count).Select(Repo).ToList()));
        public Task<GitHubTree> GetTreeAsync(string username, string repository, CancellationToken cancellationToken)
        {
            Calls++;
            var files = new List<GitHubTreeEntry> { new("a.py", "blob", "a", 10), new("b.py", "blob", "b", 10), new("Dockerfile", "blob", "d", 10) };
            if (WithManifests) { files.Add(new("package.json", "blob", repository + "p", 50)); files.Add(new("app/package.json", "blob", repository + "q", 50)); }
            return Task.FromResult(new GitHubTree(files, false));
        }
        public Task<string> GetBlobAsync(string username, string repository, string sha, CancellationToken cancellationToken)
        {
            Calls++;
            if (FailBlob) throw new GitHubApiException(429, "rate limited");
            return Task.FromResult(InvalidBlob ? "broken" : """{"dependencies":{"react":"1"}}""");
        }
    }
}
