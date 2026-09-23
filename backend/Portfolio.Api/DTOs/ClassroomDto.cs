using System.ComponentModel.DataAnnotations;

namespace Portfolio.Api.DTOs;

public sealed record CreateClassroomRequest(
    [Required, StringLength(100, MinimumLength = 2)] string Name,
    [Required, MinLength(1), MaxLength(200)] Guid[] ProfileIds);
public sealed record RenameClassroomRequest([Required, StringLength(100, MinimumLength = 2)] string Name);
public sealed record AddClassroomMembersRequest([Required, MinLength(1), MaxLength(200)] Guid[] ProfileIds);
public sealed record EvidenceCountDto(string Label, int Count);
public sealed record ClassroomSummaryDto(Guid Id, string Name, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    int MemberCount, IReadOnlyList<EvidenceCountDto> Technologies);
public sealed record ClassroomStudentDto(Guid GitHubProfileId, string Username, string? Name, string AvatarUrl,
    DateTimeOffset AddedAt, DateTimeOffset? AnalyzedAt, bool IsExpired, bool? IsComplete, IReadOnlyList<string> Skills);
public sealed record ClassroomDashboardDto(Guid Id, string Name, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    IReadOnlyList<ClassroomStudentDto> Members, IReadOnlyList<EvidenceCountDto> Technologies,
    IReadOnlyList<EvidenceCountDto> Categories, CommitActivityDto CommitActivity);
public sealed record ActivitySeriesDto(string Name, IReadOnlyList<int?> Counts);
public sealed record RecentActivityDto(IReadOnlyList<string> Months, IReadOnlyList<ActivitySeriesDto> Series,
    int RepositoryCount, int MissingPushDates, int RepositoriesInPeriod, DateTimeOffset PeriodStart, DateTimeOffset PeriodEnd);
