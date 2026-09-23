using Portfolio.Api.DTOs;
using Portfolio.Api.Services;
using Xunit;

namespace Portfolio.Api.Tests;

public sealed class InitialRecommendationTests
{
    private static SkillDto Skill(string name, int count = 1) => new(name, "Teste", "Pouca evidência", count, []);
    private readonly RecommendationService service = new();

    [Theory]
    [InlineData("C#", "Backend .NET", "ASP.NET Core", "Entity Framework Core")]
    [InlineData("Python", "Backend Python", "POO", "FastAPI/Flask")]
    [InlineData("Java", "Backend Java", "Maven/Gradle", "Spring Boot")]
    public void LanguageAloneOffersInitialTrackAndExplainsLaterProgression(string language, string name, string next, string later)
    {
        var track = Assert.Single(service.BuildTracks([Skill(language)]));
        Assert.Equal(name, track.Name);
        Assert.Contains(track.NextSteps, step => step.Topic == next);
        Assert.Contains("uma possível progressão", track.ProgressionMessage!);
        Assert.Contains(later, track.ProgressionMessage!);
        Assert.Equal(new[] { language }, track.DemonstratedSkills);
        Assert.DoesNotContain(track.NextSteps, step => step.Topic == language);
        Assert.All(track.NextSteps, step => Assert.Equal(new[] { language }, step.ConsideredSkills));
        Assert.DoesNotContain("Você não sabe", track.ProgressionMessage!);
    }

    [Fact]
    public void StrongFrontendDoesNotHideTwoInitialBackendOptions()
    {
        var skills = new[] { Skill("HTML", 8), Skill("CSS", 8), Skill("JavaScript", 8), Skill("TypeScript", 8), Skill("C#", 2), Skill("Python") };
        var tracks = service.BuildTracks(skills);
        Assert.Equal(new[] { "Frontend React", "Backend .NET", "Backend Python" }, tracks.Select(t => t.Name));
        Assert.Contains(tracks[1].NextSteps, step => step.Topic == "ASP.NET Core");
        Assert.Contains(tracks[2].NextSteps, step => step.Topic == "POO");
    }

    [Fact]
    public void InitialOptionsRemainLimitedToThreeAndRespectRecurrence()
    {
        var tracks = service.BuildTracks([Skill("HTML", 8), Skill("CSS", 8), Skill("JavaScript", 8), Skill("C#", 1), Skill("Python", 3), Skill("Java", 5)]);
        Assert.Equal(new[] { "Frontend React", "Backend Java", "Backend Python" }, tracks.Select(t => t.Name));
        Assert.All(tracks, track => Assert.True(track.NextSteps.Count > 0 || track.ProgressionMessage is not null));
    }

    [Fact]
    public void ConsolidatedDotNetKeepsItsAdvancedSuggestionsEvenWithAnotherLanguage()
    {
        var skills = new[] { "C#", "ASP.NET Core", "Web API", "Entity Framework Core", "SQL", "xUnit", "Docker", "Python" }.Select(n => Skill(n)).ToList();
        var track = Assert.Single(service.BuildTracks(skills));
        Assert.Equal(new[] { "Testes de integração", "CI/CD", "Autenticação e autorização", "Logging estruturado" }, track.NextSteps.Select(s => s.Topic));
        Assert.True(track.BaseWellRepresented);
        Assert.Contains("aprofundamento", track.ProgressionMessage!);
    }

    [Fact]
    public void InitialTracksNeverPresentObservedTechnologiesAsNew()
    {
        var names = new[] { "HTML", "CSS", "JavaScript", "TypeScript", "C#", "Python", "POO" };
        var tracks = service.BuildTracks(names.Select(n => Skill(n)).ToList());
        Assert.InRange(tracks.Count, 1, 3);
        Assert.All(tracks.SelectMany(t => t.NextSteps), step => Assert.DoesNotContain(step.Topic, names));
    }
}
