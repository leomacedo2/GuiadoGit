namespace Portfolio.Api.Services;

public sealed class GitHubRateLimitHandler(GitHubRateLimitState state) : DelegatingHandler
{
    internal static readonly HttpRequestOptionsKey<bool> SentKey = new("GitHubRequestSent");
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await state.RequestGate.WaitAsync(cancellationToken);
        try
        {
            state.EnsureAvailable();
            request.Options.Set(SentKey, true);
            var response = await base.SendAsync(request, cancellationToken);
            try { state.Observe(response); }
            catch { response.Dispose(); throw; }
            return response;
        }
        finally { state.RequestGate.Release(); }
    }
}
