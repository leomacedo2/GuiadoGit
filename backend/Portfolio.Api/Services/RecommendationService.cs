using Portfolio.Api.DTOs;

namespace Portfolio.Api.Services;

public sealed class RecommendationService
{
    public IReadOnlyList<RecommendationDto> Recommend(IReadOnlyList<SkillDto> skills)
    {
        var observed = skills.Where(s => s.RepositoryCount > 0).GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.RepositoryCount).First(), StringComparer.OrdinalIgnoreCase);
        bool Has(string name) => observed.ContainsKey(name);
        bool Ready(string[][] groups) => groups.All(group => group.Any(Has));
        var ranked = LearningTrackCatalog.All.Where(t => Ready(t.Entry))
            .Where(t => t.Name != "Frontend React" || !(Has("React Native") || Has("Expo")) || Has("HTML") || Has("CSS") || Has("React Router"))
            .Select(track =>
        {
            var matched = track.Steps.Where(s => s.Signals.Any(Has)).ToList();
            var score = matched.Count * 10 + matched.Sum(s => s.Signals.Where(Has).Max(n => Math.Min(observed[n].RepositoryCount, 5)));
            var frontier = Array.FindLastIndex(track.Steps, s => s.Milestone && s.Signals.Any(Has)
                && (!(s.Topic is "REST API" or "API REST" or "Web API") || Ready(s.Requires)));
            var candidates = track.Steps.Skip(frontier + 1).Where(s => !s.Signals.Any(Has) && Ready(s.Requires))
                .Where(s => !(s.Topic == "POO" && Has("CRUD"))).ToList();
            return new { Track = track, Score = score, Candidates = candidates };
        }).Where(t => t.Candidates.Count > 0).OrderByDescending(t => t.Score).ThenBy(t => t.Track.Name).ToList();

        var result = new List<RecommendationDto>();
        // First cover relevant tracks, then offer further eligible steps from those tracks.
        for (var round = 0; round < 3 && result.Count < 3; round++)
        foreach (var track in ranked.Where(t => t.Score * 2 >= ranked[0].Score))
        {
            if (result.Count == 3) break;
            if (round >= track.Candidates.Count) continue;
            var step = track.Candidates[round];
            if (result.Any(r => r.Topic == step.Topic)) continue;
            var signals = track.Track.Steps.SelectMany(s => s.Signals).Concat(step.Requires.SelectMany(g => g))
                .Distinct(StringComparer.OrdinalIgnoreCase).Where(Has).Select(n => observed[n]).ToList();
            var summary = string.Join(", ", signals.Select(s => $"{s.Name} ({s.RepositoryCount} repositório(s))"));
            result.Add(new(step.Topic, $"A trilha {track.Track.Name} tem evidências de {summary}. {step.Topic} é uma etapa elegível ainda não demonstrada na cobertura consultada. Ausência de evidência não significa ausência de conhecimento.", step.Practice)
            {
                Track = track.Track.Name,
                ConsideredEvidence = signals.Select(s => new RecommendationEvidenceDto(s.Name, s.RepositoryCount, s.Evidence)).ToList()
            });
        }
        return result;
    }
}
