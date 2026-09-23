using Portfolio.Api.DTOs;

namespace Portfolio.Api.Services;

public sealed partial class RecommendationService
{
    public IReadOnlyList<RecommendationDto> Recommend(IReadOnlyList<SkillDto> skills)
        => Priorities(BuildTracks(skills), skills);
}
