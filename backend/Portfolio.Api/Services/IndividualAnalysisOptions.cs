using System.ComponentModel.DataAnnotations;

namespace Portfolio.Api.Services;

public sealed class IndividualAnalysisOptions
{
    [Range(1, 1000)] public int MaxRepositories { get; set; } = 200;
    [Range(1, 3000)] public int MaxRequests { get; set; } = 600;
    [Range(1, 180)] public int TimeoutSeconds { get; set; } = 180;
    [Range(1, 32)] public int MaxManifestsPerRepository { get; set; } = 16;
}
