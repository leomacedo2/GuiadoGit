using Microsoft.EntityFrameworkCore;
using Portfolio.Api.Data;
using Portfolio.Api.DTOs;

namespace Portfolio.Api.Services;

// Deliberately depends only on persisted data, never on an analyzer or GitHub client.
public sealed class ClassroomDashboardService(PortfolioDbContext db, TimeProvider clock)
{
    public async Task<IReadOnlyList<ClassroomSummaryDto>> ListAsync(string userId, CancellationToken ct)
    {
        var classrooms = await db.Classrooms.AsNoTracking().Where(c => c.ApplicationUserId == userId)
            .OrderByDescending(c => c.UpdatedAt).ThenBy(c => c.Id).ToListAsync(ct);
        var members = await db.ClassroomMembers.AsNoTracking().Where(m => m.ApplicationUserId == userId)
            .Select(m => new { m.ClassroomId, m.GitHubProfileId }).ToListAsync(ct);
        var snapshots = await LoadSnapshots(members.Select(m => m.GitHubProfileId).Distinct().ToArray(), false, ct);
        return classrooms.Select(c =>
        {
            var ids = members.Where(m => m.ClassroomId == c.Id).Select(m => m.GitHubProfileId).ToHashSet();
            return new ClassroomSummaryDto(c.Id, c.Name, c.CreatedAt, c.UpdatedAt, ids.Count,
                Counts(snapshots.Where(s => ids.Contains(s.ProfileId)), false).Take(4).ToList());
        }).ToList();
    }

    public async Task<ClassroomDashboardDto?> GetAsync(Guid id, string userId, CancellationToken ct)
    {
        var classroom = await db.Classrooms.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id && c.ApplicationUserId == userId, ct);
        if (classroom is null) return null;
        var members = await db.ClassroomMembers.AsNoTracking().Where(m => m.ClassroomId == id && m.ApplicationUserId == userId)
            .Select(m => new { m.GitHubProfileId, m.AddedAt, m.SavedProfile.GitHubProfile.Username,
                m.SavedProfile.GitHubProfile.Nome, m.SavedProfile.GitHubProfile.AvatarUrl }).ToListAsync(ct);
        var snapshots = await LoadSnapshots(members.Select(m => m.GitHubProfileId).ToArray(), true, ct);
        var lookup = snapshots.ToDictionary(s => s.ProfileId);
        var students = members.OrderBy(m => m.Nome ?? m.Username, StringComparer.OrdinalIgnoreCase)
            .ThenBy(m => m.Username, StringComparer.OrdinalIgnoreCase).Select(m =>
            {
                lookup.TryGetValue(m.GitHubProfileId, out var snapshot);
                return new ClassroomStudentDto(m.GitHubProfileId, m.Username, m.Nome, m.AvatarUrl, m.AddedAt,
                    snapshot?.AnalyzedAt, snapshot is not null && snapshot.ExpiresAt <= clock.GetUtcNow(), snapshot?.IsComplete,
                    snapshot?.Skills.Take(4).Select(s => s.Name).ToList() ?? [])
                { Dashboard = StudentDashboard(snapshot) };
            }).ToList();
        var activity = students.Select(student => (student.GitHubProfileId, student.Dashboard.CommitActivity));
        return new(classroom.Id, classroom.Name, classroom.CreatedAt, classroom.UpdatedAt, students,
            Counts(snapshots, false), Counts(snapshots, true), CommitActivityAggregation.ForClassroom(activity, clock.GetUtcNow()));
    }

    private static ClassroomStudentDashboardDto StudentDashboard(Snapshot? snapshot)
    {
        if (snapshot is null) return new([], [], null);
        var technologies = snapshot.Skills.Where(s => s.RepositoryCount > 0)
            .Select(s => new EvidenceCountDto(s.Name, s.RepositoryCount))
            .OrderByDescending(s => s.Count).ThenBy(s => s.Label, StringComparer.OrdinalIgnoreCase).ToList();
        var categories = snapshot.Skills.GroupBy(s => s.Category, StringComparer.OrdinalIgnoreCase)
            .Select(g => new EvidenceCountDto(g.Key, g.SelectMany(s => s.RepositoryIds).Distinct().Count()))
            .Where(c => c.Count > 0).OrderByDescending(c => c.Count).ThenBy(c => c.Label, StringComparer.OrdinalIgnoreCase).ToList();
        return new(technologies, categories, CommitActivityAggregation.ReadMetadata(snapshot.MetadataJson));
    }

    private static IReadOnlyList<EvidenceCountDto> Counts(IEnumerable<Snapshot> snapshots, bool categories) => snapshots
        .SelectMany(s => s.Skills.Where(skill => skill.RepositoryCount > 0)
            .Select(skill => categories ? skill.Category : skill.Name).Distinct(StringComparer.OrdinalIgnoreCase))
        .GroupBy(name => name, StringComparer.OrdinalIgnoreCase).Select(g => new EvidenceCountDto(g.Key, g.Count()))
        .OrderByDescending(c => c.Count).ThenBy(c => c.Label, StringComparer.OrdinalIgnoreCase).ToList();

    private async Task<List<Snapshot>> LoadSnapshots(Guid[] profileIds, bool detail, CancellationToken ct)
    {
        if (profileIds.Length == 0) return [];
        var latestIds = await db.GitHubProfiles.Where(p => profileIds.Contains(p.Id))
            .Select(p => p.Analyses.OrderByDescending(a => a.AnalyzedAt).ThenByDescending(a => a.Id)
                .Select(a => (Guid?)a.Id).FirstOrDefault()).ToListAsync(ct);
        // Class lists do not need metadata. Details read the persisted commit aggregate, never GitHub.
        return await db.Analyses.AsNoTracking().AsSplitQuery().Where(a => latestIds.Contains(a.Id))
            .Select(a => new Snapshot(a.GitHubProfileId, a.AnalyzedAt, a.ExpiresAt, a.IsComplete,
                a.Skills.OrderBy(s => s.Position).Select(s => new SnapshotSkill(s.Name, s.Category, s.RepositoryCount,
                    s.Evidence.Where(e => detail).Select(e => e.RepositoryId).Distinct().ToList())).ToList(),
                detail ? a.MetadataJson : "{}")).ToListAsync(ct);
    }
    private sealed record Snapshot(Guid ProfileId, DateTimeOffset AnalyzedAt, DateTimeOffset ExpiresAt, bool IsComplete,
        List<SnapshotSkill> Skills, string MetadataJson);
    private sealed record SnapshotSkill(string Name, string Category, int RepositoryCount, List<long> RepositoryIds);
}
