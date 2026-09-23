namespace Portfolio.Api.DTOs;

// All detected series are persisted; top five is a presentation choice, after classroom aggregation.
public sealed record CommitActivityDto(
    IReadOnlyList<string> Months, IReadOnlyList<ActivitySeriesDto> Series,
    DateTimeOffset PeriodStart, DateTimeOffset PeriodEnd, DateTimeOffset CollectedAt,
    bool IsPartial, int EligibleRepositories, int InspectedRepositories, int CompletedRepositories,
    int Requests, int RequestBudget, int RepositoryLimit, int PageLimit,
    IReadOnlyList<string> Warnings, IReadOnlyList<CommitRepositoryCoverageDto> Repositories)
{
    public int MissingStudents { get; init; }
    public int StudentCount { get; init; }
}

public sealed record CommitRepositoryCoverageDto(long RepositoryId, string Name, int Pages, int Commits, string Status);
