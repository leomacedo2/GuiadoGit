namespace Portfolio.Api.Models;

public sealed record GitHubCommit(string Sha, GitHubCommitDetails? Commit);
public sealed record GitHubCommitDetails(GitHubCommitDate? Committer);
public sealed record GitHubCommitDate(DateTimeOffset? Date);
public sealed record GitHubCommitPage(IReadOnlyList<GitHubCommit> Commits, bool HasNext);
