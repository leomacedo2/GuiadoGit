using Microsoft.Extensions.Options;
using Portfolio.Api.DTOs;
using Portfolio.Api.Models;
using Portfolio.Api.Services;
using Xunit;

namespace Portfolio.Api.Tests;

public sealed class IndividualCoverageTests
{
    [Fact]
    public async Task CoversAllRepositoriesAndReusesIdenticalBlobs()
    {
        var github = new Fixture();
        var result = await new AnalysisService(github, new(), new()).AnalyzeAsync("test", default);
        Assert.Equal(25, result.InspectedRepositories);
        Assert.Equal(25, result.CompletelyInspectedRepositories);
        Assert.Equal(26, result.AdditionalRequests);
        Assert.Equal(1, github.BlobCalls);
        Assert.False(result.IsPartial);
    }

    [Fact]
    public async Task CeilingIsExplicitAndLanguagesStillCoverAllRepositories()
    {
        var result = await new AnalysisService(new Fixture(), new(), new(), Options.Create(new IndividualAnalysisOptions { MaxRepositories = 3 })).AnalyzeAsync("test", default);
        Assert.Equal(3, result.InspectedRepositories);
        Assert.Equal(25, Assert.Single(result.Skills, s => s.Name == "Python").RepositoryCount);
        Assert.True(result.IsPartial);
        Assert.Contains(result.Warnings, w => w.Contains("Teto de segurança"));
    }

    [Theory]
    [InlineData("App/wwwroot/lib/jquery.js")]
    [InlineData("WWWROOT/LIB/a/package.json")]
    [InlineData("a/node_modules/b/package.json")]
    [InlineData("vendor/a.py")]
    [InlineData("dist/a.js")]
    [InlineData("build/a.java")]
    [InlineData("bin/a.cs")]
    [InlineData("obj/a.cs")]
    [InlineData("src/a.MIN.JS")]
    public void ExcludesGeneratedPaths(string path) => Assert.False(SkillDetector.IsRelevantPath(path));

    [Fact]
    public void DetectsDatabaseProvidersWithoutGenericJavaScript()
    {
        var hits = new SkillDetector().Detect(new(1, "repo", null, null, "", DateTimeOffset.UtcNow), new(["package.json"], new Dictionary<string, string> {
            ["app.csproj"] = """<Project><ItemGroup><PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="8.0" /></ItemGroup></Project>""",
            ["wwwroot/lib/package.json"] = """{"dependencies":{"expo":"1"}}"""
        }, false));
        Assert.Contains(hits, h => h.Name == "Entity Framework Core");
        Assert.Contains(hits, h => h.Name == "SQL Server");
        Assert.DoesNotContain(hits, h => h.Name is "Expo" or "Ecossistema JavaScript" or "Banco de dados");
    }

    private sealed class Fixture : IGitHubService
    {
        public int BlobCalls { get; private set; }
        public Task<PortfolioDto> GetPortfolioAsync(string username, CancellationToken ct) => Task.FromResult(new PortfolioDto("test", null, null, "", "", 25,
            Enumerable.Range(1, 25).Select(id => new RepositoryDto(id, $"repo{id}", null, "Python", "", DateTimeOffset.UtcNow.AddDays(-id))).ToList()));
        public Task<GitHubTree> GetTreeAsync(string username, string repo, CancellationToken ct) => Task.FromResult(new GitHubTree([new("package.json", "blob", "shared-sha", 30)], false));
        public Task<string> GetBlobAsync(string username, string repo, string sha, CancellationToken ct) { BlobCalls++; return Task.FromResult("""{"dependencies":{"react":"1"}}"""); }
    }
}
