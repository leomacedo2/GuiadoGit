using Portfolio.Api.DTOs;

namespace Portfolio.Api.Services;

public interface IGitHubService
{
    int RequestCount => 0;
    Task<PortfolioDto> GetPortfolioAsync(string username, CancellationToken cancellationToken);
    Task<Models.GitHubTree> GetTreeAsync(string username, string repository, CancellationToken cancellationToken);
    Task<string> GetBlobAsync(string username, string repository, string sha, CancellationToken cancellationToken);
    Task<Models.GitHubCommitPage> GetCommitsAsync(string owner, string repository, string author,
        DateTimeOffset since, DateTimeOffset until, int page, CancellationToken cancellationToken);
}
