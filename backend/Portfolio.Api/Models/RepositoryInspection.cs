namespace Portfolio.Api.Models;

public sealed record RepositoryInspection(IReadOnlyList<string> Paths, IReadOnlyDictionary<string, string> Manifests, bool Truncated);
public sealed record GitHubTree(List<GitHubTreeEntry> Tree, bool Truncated);
public sealed record GitHubTreeEntry(string Path, string Type, string Sha, long? Size);
public sealed record GitHubBlob(string Content, string Encoding);
