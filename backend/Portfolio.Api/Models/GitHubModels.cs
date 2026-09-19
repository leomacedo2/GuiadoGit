using System.Text.Json.Serialization;

namespace Portfolio.Api.Models;

public sealed record GitHubUser(
    string Login, string? Name, string? Bio,
    [property: JsonPropertyName("avatar_url")] string AvatarUrl,
    [property: JsonPropertyName("html_url")] string HtmlUrl,
    [property: JsonPropertyName("public_repos")] int PublicRepos)
{
    public long Id { get; init; }
}

public sealed record GitHubRepository(
    long Id, string Name, string? Description, string? Language,
    [property: JsonPropertyName("html_url")] string HtmlUrl,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt);
