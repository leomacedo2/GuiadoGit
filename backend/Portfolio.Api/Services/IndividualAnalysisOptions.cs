using System.ComponentModel.DataAnnotations;

namespace Portfolio.Api.Services;

public sealed class IndividualAnalysisOptions
{
    [Range(1, 1000)] public int MaxRepositories { get; set; } = 200;
    [Range(1, 3000)] public int MaxRequests { get; set; } = 600;
    [Range(1, 180)] public int TimeoutSeconds { get; set; } = 180;
    [Range(1, 32)] public int MaxManifestsPerRepository { get; set; } = 16;
    [Range(1, 30)] public int MaxCommitHistoryRepositories { get; set; } = 25;
    [Range(1, 10)] public int MaxCommitPagesPerRepository { get; set; } = 10;
    [Range(1, 300)] public int CommitHistoryRequestBudget { get; set; } = 100;
}
