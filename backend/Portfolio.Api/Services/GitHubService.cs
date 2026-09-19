using System.Net;
using System.Text.Json;
using Portfolio.Api.DTOs;
using Portfolio.Api.Models;

namespace Portfolio.Api.Services;

public sealed class GitHubService(HttpClient client) : IGitHubService
{
    public int RequestCount { get; private set; }
    public Task<GitHubTree> GetTreeAsync(string username, string repository, CancellationToken cancellationToken)
        => GetAsync<GitHubTree>($"repos/{Uri.EscapeDataString(username)}/{Uri.EscapeDataString(repository)}/git/trees/HEAD?recursive=1", cancellationToken);

    public async Task<string> GetBlobAsync(string username, string repository, string sha, CancellationToken cancellationToken)
    {
        var blob = await GetAsync<GitHubBlob>($"repos/{Uri.EscapeDataString(username)}/{Uri.EscapeDataString(repository)}/git/blobs/{Uri.EscapeDataString(sha)}", cancellationToken);
        if (blob.Encoding != "base64") throw new JsonException("Unsupported blob encoding.");
        return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(blob.Content));
    }

    public async Task<PortfolioDto> GetPortfolioAsync(string username, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(45));
        try
        {
            var path = $"users/{Uri.EscapeDataString(username)}";
            var user = await GetAsync<GitHubUser>(path, deadline.Token);
            var repositories = new List<RepositoryDto>();
            var warnings = new List<string>();
            // Fetch every page; GitHub returns at most 100 repositories per request.
            for (var page = 1; ; page++)
            {
                List<GitHubRepository> batch;
                try
                {
                    batch = await GetAsync<List<GitHubRepository>>(
                        $"{path}/repos?type=owner&sort=updated&direction=desc&per_page=100&page={page}", deadline.Token);
                }
                catch (GitHubApiException ex) when (ex.StatusCode == 429)
                {
                    warnings.Add($"Lista de repositórios incompleta. {ex.Message}");
                    break;
                }
                repositories.AddRange(batch.Select(repo => new RepositoryDto(repo.Id, repo.Name,
                    repo.Description, repo.Language, repo.HtmlUrl, repo.UpdatedAt)));
                if (batch.Count < 100) break;
            }
            return new PortfolioDto(user.Login, user.Name, user.Bio, user.AvatarUrl,
                user.HtmlUrl, user.PublicRepos, repositories.DistinctBy(repo => repo.Id).ToList()) { GitHubId = user.Id, Warnings = warnings };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new GitHubApiException(504, "O GitHub demorou para responder. Tente novamente em instantes.");
        }
        catch (HttpRequestException)
        {
            throw new GitHubApiException(502, "Não foi possível conectar ao GitHub. Tente novamente em instantes.");
        }
        catch (JsonException)
        {
            throw new GitHubApiException(502, "O GitHub retornou dados inesperados. Tente novamente em instantes.");
        }
    }

    private async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        using var response = await SendCountedAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new GitHubApiException(404, "Usuário não encontrado no GitHub. Confira o username informado.");
        if (response.StatusCode == HttpStatusCode.TooManyRequests ||
            (response.StatusCode == HttpStatusCode.Forbidden &&
             (response.Headers.RetryAfter is not null ||
              response.Headers.TryGetValues("X-RateLimit-Remaining", out var remaining) && remaining.Contains("0"))))
            throw new GitHubApiException(429, "O limite de consultas ao GitHub foi atingido. Aguarde antes de tentar novamente.");
        if (!response.IsSuccessStatusCode)
            throw new GitHubApiException(502, "O GitHub não conseguiu atender à consulta. Tente novamente mais tarde.");
        // Bound memory even if a remote tree is unexpectedly large.
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[16384];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > 3 * 1024 * 1024) throw new HttpRequestException("GitHub response exceeded size limit.");
            buffer.Write(chunk, 0, read);
        }
        buffer.Position = 0;
        return await JsonSerializer.DeserializeAsync<T>(buffer, new JsonSerializerOptions(JsonSerializerDefaults.Web), cancellationToken)
            ?? throw new GitHubApiException(502, "O GitHub retornou uma resposta vazia.");
    }

    private async Task<HttpResponseMessage> SendCountedAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try { return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken); }
        finally
        {
            if (request.Options.TryGetValue(GitHubRateLimitHandler.SentKey, out var sent) && sent) RequestCount++;
        }
    }
}
