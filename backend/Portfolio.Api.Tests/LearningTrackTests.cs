using Portfolio.Api.DTOs;
using Portfolio.Api.Services;
using Xunit;

namespace Portfolio.Api.Tests;

public sealed class LearningTrackTests
{
    private static SkillDto Skill(string name, int count = 1) => new(name, "Synthetic", "Pouca evidência", count,
        [new(1, "example", "https://github.com/example/example", $"Sinal sintético de {name}")]);
    private static IReadOnlyList<RecommendationDto> Recommend(params string[] names) => new RecommendationService().Recommend(names.Select(n => Skill(n)).ToList());

    [Fact]
    public void FrontendFoundationHasOnlyNearbyFrontendSteps()
    {
        var result = Recommend("HTML", "CSS", "JavaScript");
        Assert.Equal(new[] { "TypeScript", "React", "Consumo de API" }, result.Select(r => r.Topic));
        Assert.All(result, r => Assert.Equal("Frontend React", r.Track));
    }

    [Fact]
    public void JavaApiHasPersistenceTestingAndPackagingSteps()
        => Assert.Equal(new[] { "JPA", "JUnit", "Docker" }, Recommend("Java", "Maven", "Spring Boot", "REST API").Select(r => r.Topic));

    [Fact]
    public void PythonCrudHasFrameworkAndTestsButNotPrematureRestOrDocker()
        => Assert.Equal(new[] { "Flask/FastAPI", "Testes com pytest" }, Recommend("Python", "SQLite", "CRUD").Select(r => r.Topic));

    [Fact]
    public void SingleLanguageDoesNotTriggerDeployment()
    {
        Assert.Equal(new[] { "POO" }, Recommend("Python").Select(r => r.Topic));
        Assert.Empty(Recommend("Docker", "CI/CD"));
    }

    [Fact]
    public void DotNetAndMobileDiffer()
    {
        var dotnet = Recommend("C#", "ASP.NET Core", "Web API");
        Assert.Equal("Entity Framework Core", dotnet[0].Topic);
        var mobile = Recommend("JavaScript", "React", "React Native", "Expo");
        Assert.Equal(new[] { "Navegação", "Consumo de API" }, mobile.Select(r => r.Topic));
        Assert.All(mobile, r => Assert.Equal("Mobile React Native", r.Track));
    }

    [Fact]
    public void EvidenceIsTraceableAndAlreadyObservedTopicsAreExcluded()
    {
        var result = Recommend("HTML", "CSS", "JavaScript", "TypeScript", "React", "React Router", "Consumo de API");
        Assert.Equal(new[] { "Testes de frontend", "Acessibilidade", "Gerenciamento de estado" }, result.Select(r => r.Topic));
        Assert.All(result, r => { Assert.NotEmpty(r.Reason); Assert.NotEmpty(r.NextStep); Assert.NotEmpty(r.ConsideredEvidence); Assert.All(r.ConsideredEvidence, e => Assert.NotEmpty(e.Evidence)); });
    }

    [Fact]
    public void RecurrenceRanksOtherwiseComparableTracksAndCapIsThree()
    {
        var service = new RecommendationService();
        var skills = new[] { Skill("Python", 5), Skill("C#", 1) };
        Assert.Equal("Backend Python", service.Recommend(skills)[0].Track);
        Assert.Equal("Backend .NET", service.Recommend([Skill("Python", 1), Skill("C#", 5)])[0].Track);
        Assert.InRange(Recommend("Java", "Maven", "Spring Boot", "REST API", "HTML", "CSS", "JavaScript", "React Native", "Expo").Count, 1, 3);
    }

    [Fact]
    public void EmptyAndZeroCountSignalsDoNotGenerateRecommendations()
    {
        Assert.Empty(Recommend());
        Assert.Empty(new RecommendationService().Recommend([Skill("Python", 0)]));
    }

    [Fact]
    public void SharedRestEvidenceDoesNotSkipPythonFramework()
    {
        var result = Recommend("Python", "CRUD", "SQLite", "REST API");
        Assert.Contains(result, r => r.Topic == "Flask/FastAPI");
        Assert.Contains(result.First(r => r.Topic == "Flask/FastAPI").ConsideredEvidence, e => e.Skill == "CRUD");
    }

    [Fact]
    public void ManifestSignalsCoverTrackSpecificTechnologies()
    {
        var hits = new SkillDetector().Detect(new(1, "sample", null, null, "", DateTimeOffset.UtcNow),
            new(["index.html", "styles.css"], new Dictionary<string, string> {
                ["package.json"] = """{"dependencies":{"react-router-dom":"1","axios":"1","expo-router":"1","@react-native-async-storage/async-storage":"1"}}""",
                ["api.csproj"] = """<Project Sdk="Microsoft.NET.Sdk.Web"/>"""
            }, false));
        foreach (var name in new[] { "HTML", "CSS", "React Router", "Consumo de API", "Expo Router", "AsyncStorage", "ASP.NET Core" })
            Assert.Contains(hits, hit => hit.Name == name);
        Assert.DoesNotContain(hits, hit => hit.Name == "Web API");
    }
}
