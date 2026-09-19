namespace Portfolio.Api.Data;

public sealed class GitHubConnection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ApplicationUserId { get; set; } = "";
    public ApplicationUser ApplicationUser { get; set; } = null!;
    public long GitHubUserId { get; set; }
    public string GitHubUsername { get; set; } = "";
    public string AvatarUrl { get; set; } = "";
    public string AccessTokenEncrypted { get; set; } = "";
    public DateTimeOffset ConnectedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? InvalidatedAt { get; set; }
}

public sealed class GitHubOAuthAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ApplicationUserId { get; set; } = "";
    public ApplicationUser ApplicationUser { get; set; } = null!;
    public string StateHash { get; set; } = "";
    public string CorrelationHash { get; set; } = "";
    public string VerifierEncrypted { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
}
