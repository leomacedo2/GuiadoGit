using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Portfolio.Api.Data;
using Portfolio.Api.Services;

namespace Portfolio.Api.OAuth;

// Only public numeric GitHub IDs and quota headers are retained, never tokens.
public sealed class UserGitHubQuotas : IDisposable
{
    private readonly MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = 10000 });
    private readonly object gate = new();
    public GitHubRateLimitState For(long id)
    {
        lock (gate)
            return cache.GetOrCreate(id, entry =>
            {
                entry.SetSize(1).SetSlidingExpiration(TimeSpan.FromHours(2));
                return new GitHubRateLimitState();
            })!;
    }
    public GitHubRateLimitSnapshot? Observed(long id) => cache.TryGetValue(id, out GitHubRateLimitState? state) ? state?.Snapshot : null;
    public void Dispose() => cache.Dispose();
}

public sealed class GitHubRequestContext(IHttpContextAccessor accessor, PortfolioDbContext db,
    GitHubOAuthConfiguration oauth, GitHubTokenProtection protection, UserGitHubQuotas quotas)
{
    private bool initialized;
    private GitHubConnection? connection;
    private string? token;
    public bool UsesUserToken => token is not null;

    public async Task PrepareAsync(HttpRequestMessage request, CancellationToken ct)
    {
        // Lazy: this runs only when a real GitHub request is needed, after shared cache lookup.
        if (!initialized)
        {
            initialized = true;
            var user = accessor.HttpContext?.User;
            var userId = user?.Identity?.IsAuthenticated == true ? user.FindFirstValue(ClaimTypes.NameIdentifier) : null;
            if (oauth.Enabled && userId is not null)
            {
                connection = await db.GitHubConnections.AsNoTracking().SingleOrDefaultAsync(c => c.ApplicationUserId == userId, ct);
                if (connection is { InvalidatedAt: null } && (connection.ExpiresAt is null || connection.ExpiresAt > DateTimeOffset.UtcNow))
                {
                    try { token = protection.Unprotect(userId, connection.AccessTokenEncrypted); }
                    catch (CryptographicException) { await InvalidateAsync(ct); }
                }
            }
        }
        if (token is not null && connection is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Options.Set(GitHubRateLimitHandler.QuotaKey, quotas.For(connection.GitHubUserId));
        }
    }

    public async Task InvalidateAsync(CancellationToken ct)
    {
        token = null;
        if (connection is not null)
            await db.GitHubConnections.Where(c => c.Id == connection.Id && c.AccessTokenEncrypted == connection.AccessTokenEncrypted)
                .ExecuteUpdateAsync(update => update.SetProperty(c => c.InvalidatedAt, DateTimeOffset.UtcNow), ct);
    }
}
