using Portfolio.Api.DTOs;
using Portfolio.Api.Services;
using Xunit;

namespace Portfolio.Api.Tests;

public sealed class ExpandedTrackTests
{
    private static SkillDto Skill(string name, int count = 1) => new(name, "Test", "Pouca evidência", count, [new(1, "sample", "", "Manifesto de teste")]);
    private static IReadOnlyList<LearningTrackDto> Tracks(params string[] names) => new RecommendationService().BuildTracks(names.Select(n => Skill(n)).ToList());

    [Fact]
    public void FrontendStartsNearbyAndAdvancedReactGetsDifferentSteps()
    {
        var beginner = Assert.Single(Tracks("HTML", "CSS", "JavaScript"));
        Assert.Equal("Frontend", beginner.Area);
        Assert.Equal(new[] { "TypeScript", "React", "Consumo de API" }, beginner.NextSteps.Select(s => s.Topic));
        var advanced = Assert.Single(Tracks("HTML", "JavaScript", "TypeScript", "React", "React Router", "Consumo de API"));
        Assert.Equal(new[] { "Testes de frontend", "Acessibilidade", "Gerenciamento de estado" }, advanced.NextSteps.Select(s => s.Topic));
        Assert.DoesNotContain("CSS", advanced.DemonstratedSkills);
    }

    [Fact]
    public void BackendPrioritizesRecurrenceAndDoesNotForceEveryLanguage()
    {
        var tracks = new RecommendationService().BuildTracks([Skill("Python", 1), Skill("C#", 5)]);
        Assert.Equal("Backend .NET", Assert.Single(tracks).Name);
        var java = Assert.Single(Tracks("Java", "Gradle", "Spring Boot", "REST API"));
        Assert.Contains(java.NextSteps, step => step.Topic == "JPA");
        Assert.DoesNotContain(java.NextSteps, step => step.Topic is "Maven" or "Maven/Gradle");
    }

    [Fact]
    public void FullStackCombinesRealSignalsAndDeduplicatesCrossCuttingSteps()
    {
        var tracks = Tracks("HTML", "JavaScript", "TypeScript", "React", "React Router", "Consumo de API", "C#", "ASP.NET Core", "Web API", "Entity Framework Core", "PostgreSQL", "xUnit", "Testes automatizados");
        Assert.Equal(new[] { "Frontend", "Backend", "Full Stack" }, tracks.Select(t => t.Area));
        var full = Assert.Single(tracks, t => t.Area == "Full Stack");
        Assert.Contains("React", full.DemonstratedSkills); Assert.Contains("ASP.NET Core", full.DemonstratedSkills);
        Assert.Contains(full.NextSteps, s => s.Topic == "Autenticação integrada");
        Assert.Contains(full.NextSteps, s => s.Topic == "Testes de integração");
        Assert.Contains(full.NextSteps, s => s.Topic == "Docker");
        Assert.DoesNotContain(tracks.SelectMany(t => t.NextSteps), s => s.Topic == "SQL");
        var topics = tracks.SelectMany(t => t.NextSteps).Select(s => s.Topic).ToList();
        Assert.Equal(topics.Count, topics.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void FullStackNeedsFrameworkAndWebContextNotJustUnrelatedLanguagesOrMobile()
    {
        Assert.DoesNotContain(Tracks("JavaScript", "HTML", "C#"), t => t.Area == "Full Stack");
        Assert.DoesNotContain(Tracks("JavaScript", "React", "Expo", "React Native", "C#", "ASP.NET Core"), t => t.Area is "Frontend" or "Full Stack");
        Assert.DoesNotContain(Tracks("Python", "SQLite"), t => t.Area == "Frontend");
    }

    [Fact]
    public void StronglyObservedSubjectsAreNeverPresentedAsNewAndCountsAreBounded()
    {
        var names = new[] { "HTML", "CSS", "JavaScript", "TypeScript", "React", "React Router", "Consumo de API", "Testes de frontend", "Acessibilidade", "Gerenciamento de estado", "Python", "FastAPI", "SQL", "pytest", "Docker" };
        var tracks = new RecommendationService().BuildTracks(names.Select(n => Skill(n, 8)).ToList());
        Assert.InRange(tracks.Count, 1, 3);
        Assert.All(tracks, track =>
        {
            Assert.InRange(track.NextSteps.Count, 0, 4);
            Assert.All(track.NextSteps, step => { Assert.DoesNotContain(step.Topic, names); Assert.NotEmpty(step.Reason); Assert.NotEmpty(step.NextStep); Assert.NotEmpty(step.ConsideredSkills); });
        });
    }

    [Fact]
    public void MissingPersistenceIsAConcreteStepAndDataTrackRequiresDataSignals()
    {
        var full = Assert.Single(Tracks("React", "JavaScript", "ASP.NET Core"), t => t.Area == "Full Stack");
        Assert.All(full.NextSteps, s => Assert.NotEmpty(s.NextStep));
        Assert.Contains(full.NextSteps, s => s.Topic == "Persistência SQL");
        Assert.Contains(Tracks("Python", "Pandas"), t => t.Area == "Dados");
        Assert.DoesNotContain(Tracks("Python"), t => t.Area == "Dados");
        Assert.Empty(Tracks());
    }

    [Fact]
    public void EquivalentSqlStepsAreNotRepeatedAcrossBackendAndDataTracks()
    {
        var tracks = Tracks("Python", "APIs Python", "Pandas", "NumPy");
        Assert.Equal(2, tracks.Count);
        Assert.Single(tracks.SelectMany(t => t.NextSteps), s => s.Topic is "SQL" or "Banco SQL" or "Persistência SQL");
    }
}
