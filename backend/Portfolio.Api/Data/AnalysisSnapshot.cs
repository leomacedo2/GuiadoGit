using System.Text.Json;
using Portfolio.Api.DTOs;

namespace Portfolio.Api.Data;

public static class AnalysisSnapshot
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static Analysis Create(AnalysisDto dto, GitHubProfile profile, DateTimeOffset now, double hours)
    {
        // PostgreSQL timestamps have microsecond precision; return that same precision immediately.
        now = new DateTimeOffset(now.UtcTicks - now.UtcTicks % 10, TimeSpan.Zero);
        return new Analysis
        {
            GitHubProfile = profile, AnalyzedAt = now, ExpiresAt = now.AddHours(hours),
            IsComplete = !dto.IsPartial, Warning = string.Join("\n", dto.Warnings),
            RepositoryCount = dto.AnalyzedRepositories, DeepInspectedRepositoryCount = dto.InspectedRepositories,
            MetadataJson = JsonSerializer.Serialize(dto with
            {
                Profile = dto.Profile with { Repositories = [] }, Skills = [], Recommendations = []
            }, Json),
            Repositories = dto.Profile.Repositories.Select((r, i) => new RepositoryAnalysis
            {
                RepositoryId = r.Id, Position = i, Name = r.Name, Description = r.Description,
                Language = r.Language, Url = r.Url, UpdatedAt = r.UpdatedAt.ToUniversalTime(), PushedAt = r.PushedAt?.ToUniversalTime()
            }).ToList(),
            Skills = dto.Skills.Select((s, i) => new SkillAnalysis
            {
                Position = i, Name = s.Name, Category = s.Category, EvidenceLevel = s.EvidenceLevel,
                RepositoryCount = s.RepositoryCount, Evidence = s.Evidence.Select((e, j) => new Evidence
                {
                    Position = j, RepositoryId = e.RepositoryId, RepositoryName = e.RepositoryName,
                    RepositoryUrl = e.RepositoryUrl, Reason = e.Reason
                }).ToList()
            }).ToList(),
            Recommendations = dto.Recommendations.Select((r, i) => new Recommendation
            {
                Position = i, Topic = r.Topic, Track = r.Track, Reason = r.Reason, NextStep = r.NextStep,
                ConsideredSkillsJson = JsonSerializer.Serialize(r.ConsideredEvidence.Select(e => e.Skill), Json)
            }).ToList()
        };
    }

    public static AnalysisDto Read(Analysis snapshot, string source = "cache")
    {
        var metadata = JsonSerializer.Deserialize<AnalysisDto>(snapshot.MetadataJson, Json)!;
        var skills = snapshot.Skills.OrderBy(s => s.Position).Select(s => new SkillDto(s.Name,
            s.Category, s.EvidenceLevel, s.RepositoryCount, s.Evidence.OrderBy(e => e.Position)
                .Select(e => new EvidenceDto(e.RepositoryId, e.RepositoryName, e.RepositoryUrl, e.Reason)).ToList())).ToList();
        return metadata with
        {
            Id = snapshot.Id, AnalyzedAt = snapshot.AnalyzedAt, ExpiresAt = snapshot.ExpiresAt,
            Source = source, CurrentGitHubRequests = source == "github" ? metadata.TotalGitHubRequests : 0,
            Profile = metadata.Profile with { Repositories = snapshot.Repositories.OrderBy(r => r.Position)
                .Select(r => new RepositoryDto(r.RepositoryId, r.Name, r.Description, r.Language, r.Url, r.UpdatedAt) { PushedAt = r.PushedAt }).ToList() },
            Skills = skills,
            Recommendations = snapshot.Recommendations.OrderBy(r => r.Position).Select(r => new RecommendationDto(r.Topic, r.Reason, r.NextStep)
            {
                Track = r.Track,
                ConsideredEvidence = JsonSerializer.Deserialize<List<string>>(r.ConsideredSkillsJson, Json)!
                    .Select(name => skills.Single(s => s.Name == name))
                    .Select(s => new RecommendationEvidenceDto(s.Name, s.RepositoryCount, s.Evidence)).ToList()
            }).ToList()
        };
    }
}
