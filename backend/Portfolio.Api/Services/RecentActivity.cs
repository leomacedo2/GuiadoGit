using Portfolio.Api.DTOs;

namespace Portfolio.Api.Services;

public sealed record RepositoryActivity(long RepositoryId, DateTimeOffset? PushedAt,
    DateTimeOffset ObservedAt, IReadOnlyList<string> Technologies);

// Distribution of the LAST observed push, not commit history or developer authorship.
public static class RecentActivity
{
    public static RecentActivityDto ForAnalysis(AnalysisDto analysis, DateTimeOffset now)
    {
        var technologies = analysis.Skills.SelectMany(s => s.Evidence.Select(e => (e.RepositoryId, s.Name)))
            .ToLookup(x => x.RepositoryId, x => x.Name);
        return Build(analysis.Profile.Repositories.Select(r => new RepositoryActivity(r.Id, r.PushedAt,
            analysis.AnalyzedAt ?? now, technologies[r.Id].ToList())), now);
    }

    public static RecentActivityDto Build(IEnumerable<RepositoryActivity> observations, DateTimeOffset now)
    {
        now = now.ToUniversalTime();
        var start = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(-11);
        var months = Enumerable.Range(0, 12).Select(i => start.AddMonths(i).ToString("yyyy-MM")).ToArray();
        // A shared repository contributes once, using its latest snapshot, never different months simultaneously.
        var repositories = observations.GroupBy(r => r.RepositoryId)
            .Select(g => g.OrderByDescending(r => r.ObservedAt).ThenByDescending(r => r.PushedAt).First()).ToList();
        var dated = repositories.Where(r => r.PushedAt >= start && r.PushedAt <= now).ToList();
        var counts = dated.SelectMany(r => r.Technologies.Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(name => new { Name = name, Month = r.PushedAt!.Value.ToUniversalTime().ToString("yyyy-MM") }))
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase).Take(5)
            .Select(g => new ActivitySeriesDto(g.Key, months.Select(month =>
            {
                var count = g.Count(x => x.Month == month);
                return count == 0 ? (int?)null : count;
            }).ToArray())).ToList();
        return new(months, counts, repositories.Count, repositories.Count(r => r.PushedAt is null), dated.Count, start, start.AddMonths(12));
    }
}
