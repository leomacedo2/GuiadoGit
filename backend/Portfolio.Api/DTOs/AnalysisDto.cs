namespace Portfolio.Api.DTOs;

public sealed record EvidenceDto(long RepositoryId, string RepositoryName, string RepositoryUrl, string Reason);
public sealed record SkillDto(string Name, string Category, string EvidenceLevel, int RepositoryCount, IReadOnlyList<EvidenceDto> Evidence);
public sealed record RecommendationEvidenceDto(string Skill, int RepositoryCount, IReadOnlyList<EvidenceDto> Evidence);
public sealed record RecommendationDto(string Topic, string Reason, string NextStep)
{
    public string Track { get; init; } = "";
    public IReadOnlyList<RecommendationEvidenceDto> ConsideredEvidence { get; init; } = [];
}
public sealed record AnalysisDto(PortfolioDto Profile, int AnalyzedRepositories, int InspectedRepositories,
    int AdditionalRequests, bool IsPartial, IReadOnlyList<string> Warnings,
    IReadOnlyList<SkillDto> Skills, IReadOnlyList<RecommendationDto> Recommendations)
{
    public Guid? Id { get; init; }
    public string Source { get; init; } = "github";
    public string? PersistenceWarning { get; init; }
    public DateTimeOffset? AnalyzedAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public int CurrentGitHubRequests { get; init; }
    public int TotalGitHubRequests { get; init; }
    public int CompletelyInspectedRepositories { get; init; }
    public int RepositorySafetyLimit { get; init; }
    public int RequestSafetyLimit { get; init; }
}
