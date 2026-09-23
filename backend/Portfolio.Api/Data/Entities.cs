using Microsoft.AspNetCore.Identity;

namespace Portfolio.Api.Data;

public sealed class ApplicationUser : IdentityUser
{
    public string Nome { get; set; } = "";
    public DateTimeOffset DataCadastro { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class GitHubProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public long GitHubId { get; set; }
    public string Username { get; set; } = "";
    public string? Nome { get; set; }
    public string AvatarUrl { get; set; } = "";
    public string? Bio { get; set; }
    public int PublicRepositoryCount { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<Analysis> Analyses { get; set; } = [];
}

// Immutable snapshot: profile/coverage metadata plus normalized collections below.
public sealed class Analysis
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GitHubProfileId { get; set; }
    public GitHubProfile GitHubProfile { get; set; } = null!;
    public DateTimeOffset AnalyzedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public bool IsComplete { get; set; }
    public string? Warning { get; set; }
    public int RepositoryCount { get; set; }
    public int DeepInspectedRepositoryCount { get; set; }
    public string MetadataJson { get; set; } = "";
    public List<RepositoryAnalysis> Repositories { get; set; } = [];
    public List<SkillAnalysis> Skills { get; set; } = [];
    public List<Recommendation> Recommendations { get; set; } = [];
}

public sealed class RepositoryAnalysis
{
    public Guid AnalysisId { get; set; }
    public long RepositoryId { get; set; }
    public int Position { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? Language { get; set; }
    public string Url { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? PushedAt { get; set; }
}

public sealed class SkillAnalysis
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AnalysisId { get; set; }
    public int Position { get; set; }
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string EvidenceLevel { get; set; } = "";
    public int RepositoryCount { get; set; }
    public List<Evidence> Evidence { get; set; } = [];
}

public sealed class Evidence
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SkillAnalysisId { get; set; }
    public int Position { get; set; }
    public long RepositoryId { get; set; }
    public string RepositoryName { get; set; } = "";
    public string RepositoryUrl { get; set; } = "";
    public string Reason { get; set; } = "";
}

public sealed class Recommendation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AnalysisId { get; set; }
    public int Position { get; set; }
    public string Topic { get; set; } = "";
    public string Track { get; set; } = "";
    public string Reason { get; set; } = "";
    public string NextStep { get; set; } = "";
    // References skills in this snapshot; evidence is stored once on each skill.
    public string ConsideredSkillsJson { get; set; } = "[]";
}

public sealed class UserSavedProfile
{
    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    public Guid GitHubProfileId { get; set; }
    public GitHubProfile GitHubProfile { get; set; } = null!;
    public DateTimeOffset SavedAt { get; set; } = DateTimeOffset.UtcNow;
}
