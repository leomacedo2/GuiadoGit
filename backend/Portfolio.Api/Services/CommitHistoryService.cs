using System.Text.Json;
using Portfolio.Api.DTOs;

namespace Portfolio.Api.Services;

// Separate request budget, shared authentication/quota handler and overall inspection deadline.
public sealed class CommitHistoryService(IGitHubService gitHub, IndividualAnalysisOptions limits)
{
    public async Task<CommitActivityDto> CollectAsync(PortfolioDto profile, IReadOnlyList<SkillDto> skills,
        DateTimeOffset now, CancellationToken deadline, CancellationToken cancellationToken)
    {
        now = now.ToUniversalTime();
        var start = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(-11);
        var months = Enumerable.Range(0, 12).Select(i => start.AddMonths(i).ToString("yyyy-MM")).ToArray();
        var technologies = skills.SelectMany(s => s.Evidence.Select(e => (e.RepositoryId, s.Name)))
            .GroupBy(x => x.RepositoryId).ToDictionary(g => g.Key, g => g.Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
        var candidates = profile.Repositories.DistinctBy(r => r.Id)
            .Where(r => !r.IsFork && !r.IsArchived && r.PushedAt >= start && r.PushedAt <= now && technologies.ContainsKey(r.Id))
            .OrderByDescending(r => r.PushedAt).ThenBy(r => r.Id).ToList();
        var counts = candidates.SelectMany(r => technologies[r.Id]).Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(name => name, _ => new int[12], StringComparer.OrdinalIgnoreCase);
        var incomplete = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var coverage = new List<CommitRepositoryCoverageDto>();
        var warnings = new HashSet<string>();
        var attempts = 0;
        var requestsBefore = gitHub.RequestCount;
        var inspected = 0;
        string? stopped = null;
        if (profile.Warnings.Count > 0) warnings.Add("A listagem de repositórios está incompleta; o histórico considera somente os repositórios disponíveis.");
        if (profile.Repositories.Any(r => !r.IsFork && !r.IsArchived && r.PushedAt is null && technologies.ContainsKey(r.Id)))
            warnings.Add("Repositórios sem data de push não puderam ser selecionados para o histórico.");

        foreach (var (repo, index) in candidates.Select((repo, index) => (repo, index)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pages = 0;
            var commits = 0;
            var status = "complete";
            // Hashes exist only for the lifetime of this repository's collection; never persisted.
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (index >= limits.MaxCommitHistoryRepositories) status = "repository-limit";
            else if (stopped is not null) status = stopped;
            else if (deadline.IsCancellationRequested) status = stopped = "deadline";
            else if (attempts >= limits.CommitHistoryRequestBudget) status = "request-budget";
            else
            {
                inspected++;
                for (var page = 1; page <= limits.MaxCommitPagesPerRepository; page++)
                {
                    if (attempts >= limits.CommitHistoryRequestBudget) { status = "request-budget"; break; }
                    if (deadline.IsCancellationRequested) { status = stopped = "deadline"; break; }
                    try
                    {
                        attempts++;
                        var batch = await gitHub.GetCommitsAsync(profile.Username, repo.Name, profile.Username, start, now, page, deadline);
                        pages++;
                        if (batch.Commits.Count > 100) status = "invalid-data";
                        foreach (var commit in batch.Commits.Take(100))
                        {
                            if (commit is null || string.IsNullOrWhiteSpace(commit.Sha) || commit.Commit?.Committer?.Date is not { } date)
                            { status = "invalid-data"; continue; }
                            date = date.ToUniversalTime();
                            if (date < start || date > now || !seen.Add(commit.Sha)) continue;
                            var month = (date.Year - start.Year) * 12 + date.Month - start.Month;
                            commits++;
                            foreach (var technology in technologies[repo.Id]) counts[technology][month]++;
                        }
                        if (!batch.HasNext) break;
                        if (page == limits.MaxCommitPagesPerRepository) status = "page-limit";
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        status = deadline.IsCancellationRequested ? "deadline" : "timeout";
                        if (deadline.IsCancellationRequested) stopped = status;
                        break;
                    }
                    catch (Exception ex) when (ex is GitHubApiException or HttpRequestException or JsonException)
                    {
                        status = ex is GitHubApiException { StatusCode: 429 } ? "rate-limit" : "unavailable";
                        if (status == "rate-limit") stopped = status;
                        break;
                    }
                }
            }
            if (status != "complete")
            {
                incomplete.UnionWith(technologies[repo.Id]);
                warnings.Add(status switch
                {
                    "repository-limit" => "Limite de repositórios do histórico atingido.",
                    "request-budget" => "Orçamento de chamadas do histórico atingido.",
                    "page-limit" => "Alguns repositórios possuem mais páginas de commits que o limite seguro.",
                    "rate-limit" => "O limite da API GitHub interrompeu o histórico; nenhuma repetição automática foi feita.",
                    "deadline" => "Prazo da análise atingido durante o histórico de commits.",
                    _ => "Não foi possível concluir o histórico de alguns repositórios; os commits já obtidos foram preservados."
                });
            }
            coverage.Add(new(repo.Id, repo.Name, pages, commits, status));
        }
        var uncertainSelection = profile.Warnings.Count > 0 || warnings.Contains("Repositórios sem data de push não puderam ser selecionados para o histórico.");
        var series = counts.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
            .Select(p => new ActivitySeriesDto(p.Key, p.Value.Select(n => n == 0 && (incomplete.Contains(p.Key) || uncertainSelection) ? (int?)null : n).ToArray())).ToList();
        return new(months, series, start, start.AddMonths(12), now, warnings.Count > 0,
            candidates.Count, inspected, coverage.Count(r => r.Status == "complete"), gitHub.RequestCount - requestsBefore,
            limits.CommitHistoryRequestBudget, limits.MaxCommitHistoryRepositories, limits.MaxCommitPagesPerRepository,
            warnings.Order().ToArray(), coverage);
    }
}
