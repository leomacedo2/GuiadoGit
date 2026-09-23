using Microsoft.Extensions.Options;
using Portfolio.Api.DTOs;
using Portfolio.Api.Models;
using Portfolio.Api.Services;
using Xunit;

namespace Portfolio.Api.Tests;

public sealed class ManifestCoverageTests
{
    private static GitHubTreeEntry File(string path, string? sha = null, long? size = 100) => new(path, "blob", sha ?? path, size);
    private static Task<AnalysisDto> Analyze(Fixture github, int manifests = 16, int requests = 600) =>
        new AnalysisService(github, new(), new(), Options.Create(new IndividualAnalysisOptions { MaxManifestsPerRepository = manifests, MaxRequests = requests })).AnalyzeAsync("test", default);

    [Fact]
    public async Task ReadsMoreThanTwoManifestsAndCompletesMonorepoWithinBudget()
    {
        var github = new Fixture();
        github.Add("frontend/package.json", """{"dependencies":{"react":"1","axios":"1"}}""");
        github.Add("backend/api.csproj", """<Project Sdk="Microsoft.NET.Sdk.Web"><ItemGroup><PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" /></ItemGroup></Project>""");
        github.Add("tools/pyproject.toml", "[project]\ndependencies = [\"fastapi>=0.1\", \"pytest\"]");
        github.Add("mobile/package.json", """{"dependencies":{"expo":"1"}}""");
        github.Paths.AddRange([File("Dockerfile"), File("docker-compose.yml"), File(".github/workflows/ci.yml"), File("frontend/tsconfig.app.json"), File("frontend/vite.config.js"), File("backend/App.sln")]);
        var result = await Analyze(github);
        Assert.False(result.IsPartial);
        Assert.Empty(result.Warnings);
        Assert.Equal(1, result.CompletelyInspectedRepositories);
        Assert.Equal(5, result.AdditionalRequests); // one tree plus four content downloads
        Assert.Equal(4, github.Blobs.Count);
        foreach (var name in new[] { "React", "Expo", "ASP.NET Core", "PostgreSQL", "APIs Python", "pytest", "Docker", "CI/CD", "TypeScript", "Vite", ".NET" })
            Assert.Contains(result.Skills, s => s.Name == name);
    }

    [Fact]
    public void RelevanceAndEcosystemDiversityWinOverAlphabeticalFirstFiles()
    {
        var files = Enumerable.Range(1, 20).Select(i => File($"a{i}/package.json")).ToList();
        files.Add(File("very/deep/backend/api.csproj"));
        files.Add(File("python/requirements.txt"));
        files.Add(File("package-lock.json"));
        files.Add(File("package.json"));
        var selected = ManifestSelector.Select(files, 3);
        Assert.Contains(selected.Files, f => f.Path == "package.json");
        Assert.Contains(selected.Files, f => f.Path.EndsWith(".csproj"));
        Assert.Contains(selected.Files, f => f.Path.EndsWith("requirements.txt"));
        Assert.DoesNotContain(selected.Files, f => f.Path.EndsWith("package-lock.json"));
        Assert.True(selected.Omitted > 0);
    }

    [Theory]
    [InlineData("node_modules/x/package.json")]
    [InlineData("vendor/pyproject.toml")]
    [InlineData("dist/package.json")]
    [InlineData("build/pom.xml")]
    [InlineData("bin/a.csproj")]
    [InlineData("obj/a.csproj")]
    [InlineData(".git/package.json")]
    [InlineData("App/wwwroot/lib/react/package.json")]
    [InlineData("third_party/react/package.json")]
    [InlineData("third-party/react/package.json")]
    [InlineData("bower_components/x/package.json")]
    [InlineData(".next/package.json")]
    public void VendorAndGeneratedManifestsAreExcluded(string path)
    {
        Assert.False(SkillDetector.IsRelevantPath(path));
        Assert.Empty(ManifestSelector.Select([File(path)], 16).Files);
    }

    [Fact]
    public async Task SixteenUniqueManifestsCompleteButSeventeenthProducesMeaningfulWarning()
    {
        var github = new Fixture();
        for (var i = 0; i < 16; i++) github.Add($"project{i}/package.json", "{}");
        Assert.False((await Analyze(github)).IsPartial);
        github.Add("project16/package.json", "{}");
        var result = await Analyze(github);
        Assert.True(result.IsPartial);
        Assert.Equal(17, result.AdditionalRequests);
        Assert.Contains(result.Warnings, w => w.Contains("limite seguro de 16") && w.Contains("1 não foram lidos"));
        Assert.DoesNotContain(result.Warnings, w => w.Contains("dois manifestos"));
    }

    [Fact]
    public async Task SharedBlobDoesNotConsumeSlotsOrCallsRepeatedly()
    {
        var github = new Fixture();
        github.Add("package.json", "{}", "shared");
        for (var i = 0; i < 25; i++) github.Paths.Add(File($"apps/{i}/package.json", "shared"));
        var result = await Analyze(github);
        Assert.False(result.IsPartial);
        Assert.Equal(2, result.AdditionalRequests);
        Assert.Single(github.Blobs);
    }

    [Fact]
    public async Task RequestBudgetStillStopsExpandedManifestInspection()
    {
        var github = new Fixture();
        for (var i = 0; i < 8; i++) github.Add($"project{i}/package.json", "{}");
        var result = await Analyze(github, requests: 4);
        Assert.True(result.IsPartial);
        Assert.Equal(4, result.AdditionalRequests);
        Assert.Equal(3, github.Blobs.Count);
        Assert.Contains(result.Warnings, w => w.Contains("Orçamento"));
    }

    [Fact]
    public async Task OversizedManifestDoesNotCrowdOutReadableDeclarations()
    {
        var github = new Fixture();
        github.Paths.Add(File("package.json", size: 100000));
        github.Add("api.csproj", "<Project/>");
        var result = await Analyze(github, manifests: 1);
        Assert.Single(github.Blobs);
        Assert.True(result.IsPartial);
        Assert.Contains(result.Warnings, w => w.Contains("64 KiB"));
    }

    [Fact]
    public void LockfileReadsOnlyRootDependenciesAndNeverTransitivePackages()
    {
        var dependencies = ManifestDependencies.Read("package-lock.json", """{"packages":{"":{"dependencies":{"react":"1"}},"node_modules/expo":{"version":"1","dependencies":{"jest":"1"}}}}""").ToList();
        Assert.Equal(["react"], dependencies);
        Assert.Single(ManifestSelector.Select([File("package-lock.json")], 16).Files);
        Assert.Throws<FormatException>(() => ManifestDependencies.Read("package-lock.json", """{"dependencies":{"expo":{}}}""").ToList());
    }

    [Theory]
    [InlineData("pyproject.toml", "[project]\ndependencies = [\"fastapi>=0.1\"]\n[project.optional-dependencies]\ntest = [\"pytest\"]", "fastapi", "pytest")]
    [InlineData("pyproject.toml", "[tool.poetry.dependencies]\npython = \"^3.12\"\nflask = \"^3\"\n[tool.poetry.group.test.dependencies]\npytest = \"*\"", "flask", "pytest")]
    [InlineData("Pipfile", "[packages]\nflask = \"*\"\n[dev-packages]\npytest = \"*\"", "flask", "pytest")]
    [InlineData("build.gradle.kts", "// implementation(\"fake:fake:1\")\nimplementation(\"org.springframework.boot:spring-boot-starter-web:3\")\ntestImplementation(\"org.junit.jupiter:junit-jupiter:5\")", "org.springframework.boot:spring-boot-starter-web", "org.junit.jupiter:junit-jupiter")]
    [InlineData("composer.json", "{\"require\":{\"laravel/framework\":\"1\"},\"require-dev\":{\"phpunit/phpunit\":\"1\"}}", "laravel/framework", "phpunit/phpunit")]
    [InlineData("Gemfile", "# gem 'fake'\ngem 'rails'\ngem 'rspec'", "rails", "rspec")]
    public void AdditionalFormatsUseDeclarationsOnly(string path, string body, string first, string second)
    {
        var dependencies = ManifestDependencies.Read(path, body).ToList();
        Assert.Equal(2, dependencies.Count);
        Assert.Contains(first, dependencies); Assert.Contains(second, dependencies);
    }

    [Fact]
    public async Task InvalidTomlPreservesPathEvidenceAndOtherManifest()
    {
        var github = new Fixture();
        github.Add("pyproject.toml", "[invalid");
        github.Add("package.json", """{"dependencies":{"react":"1"}}""");
        github.Paths.Add(File("main.py"));
        var result = await Analyze(github);
        Assert.True(result.IsPartial);
        Assert.Contains(result.Skills, s => s.Name == "Python");
        Assert.Contains(result.Skills, s => s.Name == "React");
    }

    [Theory]
    [InlineData("react", "Testes de frontend")]
    [InlineData("react-native", "Testes mobile")]
    [InlineData("express", "Testes automatizados")]
    public async Task JavascriptTestToolsRespectManifestContext(string framework, string expected)
    {
        var github = new Fixture();
        github.Add("package.json", $$$"""{"dependencies":{"{{{framework}}}":"1"},"devDependencies":{"jest":"1"}}""");
        var result = await Analyze(github);
        Assert.Contains(result.Skills, skill => skill.Name == expected);
        if (framework != "react") Assert.DoesNotContain(result.Skills, skill => skill.Name == "Testes de frontend");
    }

    private sealed class Fixture : IGitHubService
    {
        public List<GitHubTreeEntry> Paths { get; } = [];
        private Dictionary<string, string> Contents { get; } = [];
        public List<string> Blobs { get; } = [];
        public void Add(string path, string body, string? sha = null) { Paths.Add(File(path, sha)); Contents[sha ?? path] = body; }
        public Task<PortfolioDto> GetPortfolioAsync(string username, CancellationToken ct) => Task.FromResult(new PortfolioDto("test", null, null, "", "", 1, [new(1, "monorepo", null, null, "", DateTimeOffset.UtcNow)]));
        public Task<GitHubTree> GetTreeAsync(string username, string repository, CancellationToken ct) => Task.FromResult(new GitHubTree(Paths, false));
        public Task<string> GetBlobAsync(string username, string repository, string sha, CancellationToken ct) { Blobs.Add(sha); return Task.FromResult(Contents[sha]); }
    }
}
