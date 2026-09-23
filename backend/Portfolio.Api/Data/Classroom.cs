namespace Portfolio.Api.Data;

public sealed class Classroom
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ApplicationUserId { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<ClassroomMember> Members { get; set; } = [];
}

public sealed class ClassroomMember
{
    public Guid ClassroomId { get; set; }
    public Guid GitHubProfileId { get; set; }
    // Both composite FKs share this owner, preventing cross-account associations in the database.
    public string ApplicationUserId { get; set; } = "";
    public DateTimeOffset AddedAt { get; set; }
    public Classroom Classroom { get; set; } = null!;
    public UserSavedProfile SavedProfile { get; set; } = null!;
}
