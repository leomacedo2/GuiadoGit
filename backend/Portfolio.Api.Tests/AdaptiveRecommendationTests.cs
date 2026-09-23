using System.Text.Json;
using Portfolio.Api.Data;
using Portfolio.Api.DTOs;
using Portfolio.Api.Services;
using Xunit;

namespace Portfolio.Api.Tests;

public sealed class AdaptiveRecommendationTests
{
    private static readonly string[] DotNet = ["C#", "ASP.NET Core", "Web API", "REST API", "Entity Framework Core", "SQL", "SQL Server", "PostgreSQL", "xUnit", "Docker"];
    private static readonly string[] Java = ["Java", "Maven", "Spring Boot", "REST API", "JPA", "SQL", "JUnit", "Docker"];
    private static readonly string[] Python = ["Python", "FastAPI", "REST API", "SQLAlchemy", "PostgreSQL", "pytest", "Docker"];
    private static readonly string[] Frontend = ["HTML", "CSS", "JavaScript", "TypeScript", "React", "React Router", "Consumo de API", "Testes de frontend", "Acessibilidade", "Gerenciamento de estado", "Performance web"];
    private static SkillDto Skill(string name) => new(name, "Synthetic", "Boa evidência", 3, [new(1, "sample", "", "Evidência de teste")]);
    private static List<SkillDto> Skills(IEnumerable<string> names) => names.Distinct().Select(Skill).ToList();
    private static IReadOnlyList<LearningTrackDto> Tracks(IEnumerable<string> names) => new RecommendationService().BuildTracks(Skills(names));

    [Fact]
    public void CompleteDotNetBaseGetsPrioritizedDeepeningInsteadOfAnEmptyList()
    {
        var track = Assert.Single(Tracks(DotNet));
        Assert.Equal(new[] { "Testes de integração", "CI/CD", "Autenticação e autorização", "Logging estruturado" }, track.NextSteps.Select(s => s.Topic));
        Assert.True(track.BaseWellRepresented);
        Assert.Contains("aprofundamento", track.ProgressionMessage!);
        var integration = track.NextSteps[0];
        Assert.Equal("Qualidade", integration.Stage);
        Assert.Contains("WebApplicationFactory", integration.NextStep);
        foreach (var signal in new[] { "ASP.NET Core", "Entity Framework Core", "xUnit" })
            Assert.Contains(signal, integration.ConsideredSkills);
    }

    [Fact]
    public void AdvancedJavaKeepsItsStackAndOffersSecurityAndIntegration()
    {
        var track = Assert.Single(Tracks(Java));
        Assert.Equal(new[] { "Testes de integração", "CI/CD", "Spring Security", "Logging estruturado" }, track.NextSteps.Select(s => s.Topic));
        Assert.Contains("SpringBootTest", track.NextSteps[0].NextStep);
        Assert.DoesNotContain(track.NextSteps, s => s.NextStep.Contains("WebApplicationFactory"));
    }

    [Fact]
    public void AdvancedPythonBuildsOnItsApiPersistenceAndTests()
    {
        var track = Assert.Single(Tracks(Python));
        Assert.Equal(new[] { "CI/CD", "Testes de integração", "Autenticação e autorização", "Logging estruturado" }, track.NextSteps.Select(s => s.Topic));
        Assert.Contains("pytest", track.NextSteps.Single(s => s.Topic == "Testes de integração").NextStep);
        Assert.True(track.BaseWellRepresented);
    }

    [Fact]
    public void AdvancedFrontendGetsApplicationQualityAndArchitectureNotBackendInfrastructure()
    {
        var track = Assert.Single(Tracks(Frontend));
        Assert.Equal(new[] { "CI/CD frontend", "Arquitetura de componentes", "Formulários e validação", "Segurança frontend" }, track.NextSteps.Select(s => s.Topic));
        Assert.DoesNotContain(track.NextSteps, s => s.Topic is "Docker" or "Redis" or "SSR/SSG");
        Assert.Contains(Assert.Single(Tracks(Frontend.Concat(["Arquitetura de componentes"]))).NextSteps, s => s.Topic == "Design system");
    }

    [Fact]
    public void FullStackUsesIntegrationChallengesAndDeduplicatesConceptsAcrossTracks()
    {
        var names = Frontend.Concat(DotNet).Concat(["Autenticação integrada", "Testes de integração", "CI/CD", "Deploy integrado"]);
        var tracks = Tracks(names);
        var full = Assert.Single(tracks, t => t.Area == "Full Stack");
        Assert.Equal(new[] { "Contratos API", "Erros entre camadas", "Docker Compose", "Cache integrado" }, full.NextSteps.Select(s => s.Topic));
        Assert.True(full.BaseWellRepresented);
        var steps = tracks.SelectMany(t => t.NextSteps).ToList();
        Assert.Equal(steps.Count, steps.Select(s => s.Topic).Distinct().Count());
        Assert.DoesNotContain(steps, s => s.Topic is "Cache" or "Tratamento global de erros" or "Autenticação e autorização");
        Assert.DoesNotContain(Tracks(DotNet), t => t.Area == "Full Stack");
    }

    [Theory]
    [InlineData("WebApplicationFactory", "Testes de integração")]
    [InlineData("Identity", "Autenticação e autorização")]
    [InlineData("JWT", "Autenticação e autorização")]
    [InlineData("Serilog", "Logging estruturado")]
    [InlineData("OpenTelemetry", "Logging estruturado")]
    [InlineData("CI/CD", "CI/CD")]
    [InlineData("Autenticação e autorização", "Autenticação e autorização")]
    public void ObservedAliasesAreNotSuggestedAgain(string signal, string excluded)
        => Assert.DoesNotContain(Tracks(DotNet.Append(signal)).SelectMany(t => t.NextSteps), s => s.Topic == excluded);

    [Fact]
    public void ObservedAuthenticationAlsoSuppressesEquivalentFullStackSuggestion()
        => Assert.DoesNotContain(Tracks(Frontend.Concat(DotNet).Append("Identity")).SelectMany(t => t.NextSteps),
            s => s.Topic is "Autenticação integrada" or "Autenticação e autorização");

    [Fact]
    public void DeeperStepsRequireObservedPrerequisitesNotOtherSuggestions()
    {
        var baseTrack = Assert.Single(Tracks(DotNet));
        Assert.DoesNotContain(baseTrack.NextSteps, s => s.Topic is "Redis" or "Mensageria" or "Arquitetura em camadas" or "Observabilidade");
        var deeper = Assert.Single(Tracks(DotNet.Concat(["Testes de integração", "Autenticação", "CI/CD", "Logging estruturado"])));
        Assert.Contains(deeper.NextSteps, s => s.Topic == "Arquitetura em camadas");
        Assert.Contains(deeper.NextSteps, s => s.Topic == "Observabilidade");
        Assert.DoesNotContain(deeper.NextSteps, s => s.Topic == "Mensageria");
        Assert.DoesNotContain(Tracks(DotNet.Except(["Docker"])).SelectMany(t => t.NextSteps), s => s.Topic == "CI/CD");
        Assert.Equal("POO", Assert.Single(Assert.Single(Tracks(["Python"])).NextSteps).Topic);
    }

    [Fact]
    public void CountsReasonsAndPracticesRemainBoundedContextualAndEvidenceBased()
    {
        foreach (var names in new[] { DotNet, Java, Python, Frontend, Frontend.Concat(DotNet).ToArray() })
        {
            var service = new RecommendationService();
            var skills = Skills(names);
            var tracks = service.BuildTracks(skills);
            Assert.InRange(tracks.Count, 1, 3);
            Assert.InRange(service.Recommend(skills).Count, 1, 3);
            Assert.Equal(JsonSerializer.Serialize(service.Priorities(tracks, skills)), JsonSerializer.Serialize(service.Recommend(skills)));
            Assert.All(tracks, track =>
            {
                Assert.InRange(track.NextSteps.Count, 2, 4);
                Assert.All(track.NextSteps, step =>
                {
                    Assert.DoesNotContain(step.Topic, names);
                    Assert.NotEmpty(step.Stage!);
                    Assert.True(step.NextStep.Length > 40);
                    Assert.Contains("Não encontramos evidência", step.Reason);
                    Assert.DoesNotContain("Você não sabe", step.Reason);
                    Assert.DoesNotContain("Você precisa aprender", step.Reason);
                    Assert.NotEmpty(step.ConsideredSkills);
                    Assert.All(step.ConsideredSkills, name => { Assert.Contains(name, names); Assert.Contains(name, step.Reason); });
                });
            });
        }
    }

    [Fact]
    public void OrderingIsDeterministicRegardlessOfInputOrder()
    {
        var names = DotNet.Concat(["CI/CD", "Autenticação", "Serilog", "Testes de integração"]).ToArray();
        Assert.Equal(JsonSerializer.Serialize(Tracks(names)), JsonSerializer.Serialize(Tracks(names.Reverse())));
    }

    [Fact]
    public void NewProgressionRoundTripsInExistingMetadataAndLegacyFieldsAreOptional()
    {
        var skills = Skills(DotNet);
        var service = new RecommendationService();
        var tracks = service.BuildTracks(skills);
        var dto = new AnalysisDto(new("sample", null, null, "", "", 0, []), 0, 0, 0, false, [], skills, service.Priorities(tracks, skills)) { LearningTracks = tracks };
        var snapshot = AnalysisSnapshot.Create(dto, new GitHubProfile { Username = "sample" }, DateTimeOffset.UtcNow, 6);
        var read = AnalysisSnapshot.Read(snapshot);
        Assert.Equal(JsonSerializer.Serialize(tracks), JsonSerializer.Serialize(read.LearningTracks));
        Assert.Equal(JsonSerializer.Serialize(dto.Recommendations), JsonSerializer.Serialize(read.Recommendations));
        var legacy = JsonSerializer.Deserialize<LearningTrackDto>("""{"name":"Backend .NET","area":"Backend","demonstratedSkills":["C#"],"nextSteps":[]}""", new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.False(legacy.BaseWellRepresented);
        Assert.Null(legacy.ProgressionMessage);
    }
}

