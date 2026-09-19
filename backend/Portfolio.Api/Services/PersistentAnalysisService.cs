using Microsoft.EntityFrameworkCore;
using Portfolio.Api.Data;
using Portfolio.Api.DTOs;

namespace Portfolio.Api.Services;

public sealed class AnalysisLocks
{
    private readonly SemaphoreSlim[] locks = Enumerable.Range(0, 128).Select(_ => new SemaphoreSlim(1)).ToArray();
    public SemaphoreSlim For(string username) => locks[(uint)StringComparer.Ordinal.GetHashCode(username) % locks.Length];
}

public sealed class PersistentAnalysisService(PortfolioDbContext db, AnalysisService analyzer,
    IConfiguration configuration, AnalysisLocks locks, TimeProvider clock)
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(configuration.GetConnectionString("Portfolio"));

    public IQueryable<Analysis> Snapshots => db.Analyses.AsNoTracking().AsSplitQuery()
        .Include(a => a.Repositories).Include(a => a.Skills).ThenInclude(s => s.Evidence).Include(a => a.Recommendations);

    public async Task<AnalysisDto> GetAsync(string username, bool force, CancellationToken ct)
    {
        username = username.Trim().ToLowerInvariant();
        if (!IsConfigured)
        {
            var transient = await analyzer.AnalyzeAsync(username, ct);
            return transient with { AnalyzedAt = clock.GetUtcNow(), CurrentGitHubRequests = transient.TotalGitHubRequests,
                PersistenceWarning = "Banco não configurado: consulta disponível, mas sem cache ou salvamento. Configure o PostgreSQL no backend." };
        }
        var requestedAt = clock.GetUtcNow();
        var gate = locks.For(username);
        await gate.WaitAsync(ct);
        try
        {
            var existing = await Snapshots.Where(a => a.GitHubProfile.Username == username)
                .OrderByDescending(a => a.AnalyzedAt).FirstOrDefaultAsync(ct);
            // Concurrent refresh requests reuse the snapshot completed after they started.
            if (existing is not null && existing.ExpiresAt > clock.GetUtcNow() &&
                (!force || existing.AnalyzedAt >= requestedAt))
                return AnalysisSnapshot.Read(existing);

            AnalysisDto result;
            var requestsBefore = analyzer.GitHubRequestCount;
            try { result = await analyzer.AnalyzeAsync(username, ct); }
            catch (GitHubApiException ex) when (existing is not null)
            {
                return AnalysisSnapshot.Read(existing) with
                {
                    CurrentGitHubRequests = analyzer.GitHubRequestCount - requestsBefore,
                    PersistenceWarning = $"Não foi possível atualizar: {ex.Message} Exibindo a análise anterior, de {existing.AnalyzedAt:u}."
                };
            }
            var profile = await db.GitHubProfiles.SingleOrDefaultAsync(p => p.GitHubId == result.Profile.GitHubId, ct);
            profile ??= new GitHubProfile { GitHubId = result.Profile.GitHubId };
            profile.Username = result.Profile.Username.ToLowerInvariant();
            profile.Nome = result.Profile.Name;
            profile.AvatarUrl = result.Profile.AvatarUrl;
            profile.Bio = result.Profile.Bio;
            profile.PublicRepositoryCount = result.Profile.PublicRepositories;
            profile.UpdatedAt = clock.GetUtcNow();
            var hours = configuration.GetValue<double>("GitHubAnalysisCacheHours", 6);
            var snapshot = AnalysisSnapshot.Create(result, profile, clock.GetUtcNow(), hours);
            db.Analyses.Add(snapshot);
            // One transaction commits the entire graph; previous snapshots remain intact.
            await db.SaveChangesAsync(ct);
            return AnalysisSnapshot.Read(snapshot, "github");
        }
        finally { gate.Release(); }
    }
}
