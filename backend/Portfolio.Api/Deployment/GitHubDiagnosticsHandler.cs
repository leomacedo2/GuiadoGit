namespace Portfolio.Api.Deployment;

public sealed class GitHubDiagnosticsHandler(ILogger<GitHubDiagnosticsHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            var response = await base.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                logger.LogWarning("Integração GitHub retornou HTTP {Status}.", (int)response.StatusCode);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogWarning("Integração GitHub interrompida. Tipo: {Type}", ex.GetType().Name);
            throw;
        }
    }
}
