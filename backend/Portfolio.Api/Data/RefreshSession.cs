namespace Portfolio.Api.Data;

public sealed class RefreshSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ApplicationUserId { get; set; } = "";
    public ApplicationUser ApplicationUser { get; set; } = null!;
    public string TokenHash { get; set; } = "";
    public string SecurityStamp { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}
