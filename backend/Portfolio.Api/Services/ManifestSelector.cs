using Portfolio.Api.Models;

namespace Portfolio.Api.Services;

public sealed record ManifestSelection(IReadOnlyList<GitHubTreeEntry> Files, int Omitted, IReadOnlyList<string> Unreadable);

// Read dependency declarations, not configuration/source files whose paths already supply the signal.
public static class ManifestSelector
{
    public const int MaxBytes = 65536;
    public static string? Family(string path) => Path.GetFileName(path).ToLowerInvariant() switch
    {
        "package.json" or "package-lock.json" => "npm",
        "pom.xml" => "maven",
        "build.gradle" or "build.gradle.kts" => "gradle",
        "requirements.txt" or "pyproject.toml" or "pipfile" => "python",
        "composer.json" => "php",
        "gemfile" => "ruby",
        _ when path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) => "dotnet",
        _ => null
    };

    public static ManifestSelection Select(IEnumerable<GitHubTreeEntry> entries, int limit)
    {
        var files = entries.Where(f => f.Type == "blob" && SkillDetector.IsRelevantPath(f.Path)).ToList();
        var paths = files.Select(f => f.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidates = files.Where(f => Family(f.Path) is not null)
            // A lockfile is generated and mostly transitive; only use its root declarations as fallback.
            .Where(f => !f.Path.EndsWith("package-lock.json", StringComparison.OrdinalIgnoreCase) ||
                !paths.Contains(f.Path[..^"package-lock.json".Length] + "package.json"))
            .OrderBy(f => f.Path.EndsWith("package-lock.json", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .ThenBy(f => f.Path.Count(c => c == '/')).ThenBy(f => f.Path, StringComparer.Ordinal)
            .GroupBy(f => (Family(f.Path), f.Sha)).Select(g => g.First()).ToList();
        var unreadable = candidates.Where(f => f.Size is null or > MaxBytes).Select(f => f.Path).ToList();
        // Round-robin ecosystems before deeper copies of the same manifest type in monorepos.
        var groups = candidates.Where(f => f.Size is >= 0 and <= MaxBytes)
            .GroupBy(f => Family(f.Path)).Select(g => new Queue<GitHubTreeEntry>(g)).ToList();
        var ranked = new List<GitHubTreeEntry>();
        while (groups.Any(g => g.Count > 0))
            foreach (var group in groups.Where(g => g.Count > 0)) ranked.Add(group.Dequeue());
        return new(ranked.Take(limit).ToList(), Math.Max(0, ranked.Count - limit), unreadable);
    }
}
