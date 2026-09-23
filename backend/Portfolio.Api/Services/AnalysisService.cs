using System.Text.Json;
using System.Xml;
using Portfolio.Api.DTOs;
using Portfolio.Api.Models;
using Microsoft.Extensions.Options;

namespace Portfolio.Api.Services;

public sealed class AnalysisService(IGitHubService gitHub, SkillDetector detector, RecommendationService recommendations,
    IOptions<IndividualAnalysisOptions>? options = null, TimeProvider? clock = null)
{
    public int GitHubRequestCount => gitHub.RequestCount;
    private readonly IndividualAnalysisOptions limits = options?.Value ?? new();

    public async Task<AnalysisDto> AnalyzeAsync(string username, CancellationToken cancellationToken)
    {
        var initialRequests = gitHub.RequestCount;
        var now = (clock ?? TimeProvider.System).GetUtcNow();
        var profile = await gitHub.GetPortfolioAsync(username, cancellationToken);
        var blobCache = new Dictionary<string, string>(StringComparer.Ordinal);
        var completed = 0;
        var evidence = new List<(string Name, string Category, EvidenceDto Evidence)>();
        var warnings = new List<string>(profile.Warnings);
        var inspected = 0;
        var attempted = 0;
        var requests = 0;
        var stop = profile.Warnings.Count > 0;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(limits.TimeoutSeconds));
        if (profile.Repositories.Count > limits.MaxRepositories) warnings.Add($"Teto de segurança: até {limits.MaxRepositories} repositórios inspecionados por atualização; linguagem principal considerada em todos.");
        foreach (var repo in profile.Repositories.DistinctBy(r => r.Id).OrderByDescending(r => r.UpdatedAt))
        {
            cancellationToken.ThrowIfCancellationRequested();
            RepositoryInspection? inspection = null;
            if (!stop && attempted < limits.MaxRepositories && requests < limits.MaxRequests)
            {
                try
                {
                    attempted++;
                    var warningCount = warnings.Count;
                    requests++;
                    var tree = await gitHub.GetTreeAsync(profile.Username, repo.Name, deadline.Token);
                    inspected++;
                    var files = tree.Tree.Where(f => f.Type == "blob" && SkillDetector.IsRelevantPath(f.Path)).ToList();
                    var manifests = new Dictionary<string, string>();
                    inspection = new(files.Select(f => f.Path).ToList(), manifests, tree.Truncated);
                    var selection = ManifestSelector.Select(files, limits.MaxManifestsPerRepository);
                    if (tree.Truncated) warnings.Add($"{repo.Name}: árvore de arquivos truncada pelo GitHub.");
                    if (selection.Omitted > 0) warnings.Add($"{repo.Name}: a análise encontrou mais arquivos relevantes do que o limite seguro de {limits.MaxManifestsPerRepository} manifestos; {selection.Omitted} não foram lidos. Algumas evidências podem não ter sido consideradas.");
                    foreach (var path in selection.Unreadable) warnings.Add($"{repo.Name}: manifesto {path} excede 64 KiB ou não informa tamanho; conteúdo não inspecionado.");
                    foreach (var manifest in selection.Files)
                    {
                        try
                        {
                            if (!blobCache.TryGetValue(manifest.Sha, out var content))
                            {
                                if (requests >= limits.MaxRequests) { warnings.Add("Orçamento de chamadas atingido; alguns manifestos não foram lidos."); break; }
                                requests++;
                                content = await gitHub.GetBlobAsync(profile.Username, repo.Name, manifest.Sha, deadline.Token);
                                blobCache.Add(manifest.Sha, content);
                            }
                            // Validate individually so a malformed manifest does not discard other evidence.
                            detector.Detect(repo, new([], new Dictionary<string, string> { [manifest.Path] = content }, false));
                            manifests.Add(manifest.Path, content);
                        }
                        catch (Exception ex) when (ex is JsonException or XmlException or FormatException or InvalidOperationException or System.Text.RegularExpressions.RegexMatchTimeoutException)
                        { warnings.Add($"{repo.Name}: manifesto {manifest.Path} inválido; ignorado."); }
                    }
                    if (warnings.Count == warningCount) completed++;
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                { warnings.Add("Prazo de inspeção atingido; resultado parcial preservado."); stop = true; }
                catch (Exception ex) when (ex is GitHubApiException or HttpRequestException or JsonException or FormatException)
                {
                    warnings.Add($"{repo.Name}: inspeção incompleta; evidências já coletadas foram preservadas.");
                    if (ex is GitHubApiException { StatusCode: 429 }) warnings.Add(ex.Message);
                    if (ex is GitHubApiException { StatusCode: 429 or 502 }) stop = true;
                }
            }
            foreach (var hit in detector.Detect(repo, inspection))
                evidence.Add((hit.Name, hit.Category, new(repo.Id, repo.Name, repo.Url, hit.Reason)));
        }
        if (requests >= limits.MaxRequests && inspected < Math.Min(limits.MaxRepositories, profile.Repositories.Count)) warnings.Add("Orçamento de chamadas atingido antes de concluir a inspeção de arquivos.");
        var skills = evidence.GroupBy(x => (x.Name, x.Category)).Select(group =>
        {
            var count = group.Select(x => x.Evidence.RepositoryId).Distinct().Count();
            var level = count switch { 1 => "Pouca evidência", 2 => "Em desenvolvimento", <= 4 => "Boa evidência", _ => "Forte evidência" };
            return new SkillDto(group.Key.Name, group.Key.Category, level, count, group.Select(x => x.Evidence).Distinct().ToList());
        }).OrderByDescending(s => s.RepositoryCount).ThenBy(s => s.Name).ToList();
        var tracks = recommendations.BuildTracks(skills);
        var commits = await new CommitHistoryService(gitHub, limits).CollectAsync(profile, skills, now, deadline.Token, cancellationToken);
        return new(profile, profile.Repositories.Count, inspected, requests, warnings.Count > 0,
            warnings.Distinct().ToList(), skills, recommendations.Priorities(tracks, skills))
        {
            TotalGitHubRequests = gitHub.RequestCount - initialRequests,
            CompletelyInspectedRepositories = completed,
            RepositorySafetyLimit = limits.MaxRepositories,
            RequestSafetyLimit = limits.MaxRequests,
            ManifestSafetyLimit = limits.MaxManifestsPerRepository,
            AnalysisVersion = 2,
            LearningTracks = tracks,
            CommitActivity = commits
        };
    }
}
