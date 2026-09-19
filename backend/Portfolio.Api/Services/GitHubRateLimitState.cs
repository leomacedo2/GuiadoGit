using System.Net;
using System.Net.Http.Headers;

namespace Portfolio.Api.Services;

public sealed record GitHubRateLimitSnapshot(long? Limit, long? Remaining, DateTimeOffset? ResetAt,
    DateTimeOffset? ObservedAt, DateTimeOffset? BlockedUntil);

// Only quota metadata is retained, never credentials or profile data.
public sealed class GitHubRateLimitState
{
    private readonly object gate = new();
    private GitHubRateLimitSnapshot snapshot = new(null, null, null, null, null);
    // Serialize outbound requests across typed clients to honor exhausted quota immediately.
    internal SemaphoreSlim RequestGate { get; } = new(1, 1);

    public GitHubRateLimitSnapshot Snapshot { get { lock (gate) return snapshot; } }

    public void EnsureAvailable()
    {
        var current = Snapshot;
        if (current.BlockedUntil is { } until && until > DateTimeOffset.UtcNow)
            throw new GitHubApiException(429, $"O limite de consultas ao GitHub foi atingido. Tente novamente após {until:dd/MM/yyyy HH:mm:ss} UTC. Nenhuma nova chamada foi enviada.");
    }

    public void Observe(HttpResponseMessage response)
    {
        static long? Number(HttpResponseHeaders headers, string name) =>
            headers.TryGetValues(name, out var values) && long.TryParse(values.FirstOrDefault(), out var number) && number >= 0 ? number : null;
        var limit = Number(response.Headers, "x-ratelimit-limit");
        var remaining = Number(response.Headers, "x-ratelimit-remaining");
        var seconds = Number(response.Headers, "x-ratelimit-reset");
        DateTimeOffset? reset = seconds is >= 0 and <= 253402300799 ? DateTimeOffset.FromUnixTimeSeconds(seconds.Value) : null;
        var now = DateTimeOffset.UtcNow;
        var exhausted = remaining == 0;
        var throttled = response.StatusCode == HttpStatusCode.TooManyRequests ||
            response.StatusCode == HttpStatusCode.Forbidden && (exhausted || response.Headers.RetryAfter is not null);
        DateTimeOffset? blocked = null;
        if (exhausted || throttled)
        {
            // Missing/invalid reset headers still produce a finite cooldown, not a retry loop.
            blocked = exhausted && reset > now ? reset : now.AddMinutes(1);
            var retry = response.Headers.RetryAfter?.Date ?? (response.Headers.RetryAfter?.Delta is { } delta ? now.Add(delta) : null);
            if (retry > blocked) blocked = retry;
        }
        lock (gate)
        {
            snapshot = new(limit, remaining, reset, now, blocked);
        }
        if (throttled) EnsureAvailable();
    }
}
