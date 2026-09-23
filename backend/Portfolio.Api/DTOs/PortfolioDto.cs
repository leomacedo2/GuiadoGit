namespace Portfolio.Api.DTOs;

public sealed record PortfolioDto(string Username, string? Name, string? Bio,
    string AvatarUrl, string ProfileUrl, int PublicRepositories,
    IReadOnlyList<RepositoryDto> Repositories)
{
    public long GitHubId { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

public sealed record RepositoryDto(long Id, string Name, string? Description,
    string? Language, string Url, DateTimeOffset UpdatedAt)
{
    public DateTimeOffset? PushedAt { get; init; }
    public bool IsFork { get; init; }
    public bool IsArchived { get; init; }
}
