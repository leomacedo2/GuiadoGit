namespace Portfolio.Api.Services;

public sealed class GitHubRateLimitHandler(GitHubRateLimitState state) : DelegatingHandler
{
    internal static readonly HttpRequestOptionsKey<bool> SentKey = new("GitHubRequestSent");
    public static readonly HttpRequestOptionsKey<GitHubRateLimitState> QuotaKey = new("GitHubQuotaContext");
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var quota = request.Options.TryGetValue(QuotaKey, out var contextual) ? contextual : state;
        await quota.RequestGate.WaitAsync(cancellationToken);
        try
        {
            quota.EnsureAvailable();
            request.Options.Set(SentKey, true);
            var response = await base.SendAsync(request, cancellationToken);
            try { quota.Observe(response); }
            catch { response.Dispose(); throw; }
            return response;
        }
        finally { quota.RequestGate.Release(); }
    }
}
