using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Portfolio.Api.Data;
using Portfolio.Api.Services;

namespace Portfolio.Api.Controllers;

[ApiController, Authorize, Route("api/me/profiles")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SavedProfilesController(PortfolioDbContext db, PersistentAnalysisService persistence, TimeProvider clock) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var saved = await db.UserSavedProfiles.AsNoTracking().Where(s => s.UserId == UserId)
            .OrderByDescending(s => s.SavedAt).Select(s => new
            {
                s.GitHubProfileId, s.SavedAt, s.GitHubProfile.Username, s.GitHubProfile.Nome, s.GitHubProfile.AvatarUrl,
                Latest = s.GitHubProfile.Analyses.OrderByDescending(a => a.AnalyzedAt).Select(a => new
                {
                    a.Id, a.AnalyzedAt, a.ExpiresAt, a.IsComplete,
                    Skills = a.Skills.OrderBy(x => x.Position).Take(4).Select(x => x.Name).ToList()
                }).FirstOrDefault()
            }).ToListAsync(ct);
        return Ok(saved.OrderByDescending(s => s.Latest?.AnalyzedAt ?? DateTimeOffset.MinValue)
            .ThenByDescending(s => s.SavedAt).ThenBy(s => s.Username, StringComparer.OrdinalIgnoreCase)
            .Select(s => new { s.GitHubProfileId, s.SavedAt, s.Username, s.Nome, s.AvatarUrl,
            s.Latest, IsExpired = s.Latest is null || s.Latest.ExpiresAt <= clock.GetUtcNow() }));
    }

    [HttpPut("{analysisId:guid}")]
    public async Task<IActionResult> Save(Guid analysisId, CancellationToken ct)
    {
        var profileId = await db.Analyses.Where(a => a.Id == analysisId).Select(a => (Guid?)a.GitHubProfileId).SingleOrDefaultAsync(ct);
        if (profileId is null) return NotFound();
        // Atomic, idempotent even when the same user saves from two tabs.
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO portfolio.\"UserSavedProfiles\" (\"UserId\", \"GitHubProfileId\", \"SavedAt\") VALUES ({UserId}, {profileId.Value}, {clock.GetUtcNow()}) ON CONFLICT DO NOTHING", ct);
        return NoContent();
    }

    [HttpGet("{profileId:guid}/analysis")]
    public async Task<IActionResult> Open(Guid profileId, CancellationToken ct)
    {
        if (!await db.UserSavedProfiles.AnyAsync(s => s.UserId == UserId && s.GitHubProfileId == profileId, ct)) return NotFound();
        var snapshot = await persistence.Snapshots.Where(a => a.GitHubProfileId == profileId)
            .OrderByDescending(a => a.AnalyzedAt).FirstOrDefaultAsync(ct);
        return snapshot is null ? NotFound() : Ok(AnalysisSnapshot.Read(snapshot));
    }

    [HttpDelete("{profileId:guid}")]
    public async Task<IActionResult> Remove(Guid profileId, CancellationToken ct)
    {
        await db.UserSavedProfiles.Where(s => s.UserId == UserId && s.GitHubProfileId == profileId).ExecuteDeleteAsync(ct);
        return NoContent();
    }
}
