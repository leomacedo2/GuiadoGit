using System.Text.Json;
using Portfolio.Api.DTOs;

namespace Portfolio.Api.Services;

public static class CommitActivityAggregation
{
    public static CommitActivityDto? ReadMetadata(string metadata)
    {
        using var document = JsonDocument.Parse(metadata);
        return document.RootElement.TryGetProperty("commitActivity", out var activity) && activity.ValueKind != JsonValueKind.Null
            ? activity.Deserialize<CommitActivityDto>(new JsonSerializerOptions(JsonSerializerDefaults.Web)) : null;
    }

    public static CommitActivityDto ForClassroom(IEnumerable<(Guid ProfileId, CommitActivityDto? Activity)> students, DateTimeOffset now)
    {
        var unique = students.DistinctBy(s => s.ProfileId).ToList();
        var available = unique.Where(s => s.Activity is not null).Select(s => s.Activity!).ToList();
        var missing = unique.Count - available.Count;
        now = now.ToUniversalTime();
        var start = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(-11);
        var months = Enumerable.Range(0, 12).Select(i => start.AddMonths(i).ToString("yyyy-MM")).ToArray();
        var partial = missing > 0 || available.Any(a => a.IsPartial || !months.All(a.Months.Contains));
        var names = available.SelectMany(a => a.Series.Select(s => s.Name)).Distinct(StringComparer.OrdinalIgnoreCase);
        var series = names.Order(StringComparer.OrdinalIgnoreCase).Select(name => new ActivitySeriesDto(name, months.Select(month =>
        {
            var sum = 0;
            var unknown = missing > 0;
            foreach (var activity in available)
            {
                var index = activity.Months.ToList().IndexOf(month);
                if (index < 0) { unknown = true; continue; }
                var technology = activity.Series.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
                // Absent technology in a completely collected profile contributes zero.
                if (technology is null) { unknown |= activity.IsPartial; continue; }
                var value = technology.Counts.ElementAtOrDefault(index);
                if (value is null) unknown = true;
                else sum += value.Value;
            }
            return sum == 0 && unknown ? (int?)null : sum;
        }).ToArray())).ToList();
        return new(months, series, start, start.AddMonths(12), available.Count > 0 ? available.Min(a => a.CollectedAt) : now,
            partial, available.Sum(a => a.EligibleRepositories), available.Sum(a => a.InspectedRepositories),
            available.Sum(a => a.CompletedRepositories), 0, 0, 0, 0,
            partial ? ["O histórico agregado usa somente os dados disponíveis nos snapshots; valores positivos podem ser contagens parciais."] : [], [])
        { MissingStudents = missing, StudentCount = unique.Count };
    }
}
