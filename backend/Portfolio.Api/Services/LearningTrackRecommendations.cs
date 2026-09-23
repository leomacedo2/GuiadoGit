using Portfolio.Api.DTOs;

namespace Portfolio.Api.Services;

public sealed partial class RecommendationService
{
    public IReadOnlyList<LearningTrackDto> BuildTracks(IReadOnlyList<SkillDto> skills)
    {
        var observed = skills.Where(s => s.RepositoryCount > 0).GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.MaxBy(s => s.RepositoryCount)!, StringComparer.OrdinalIgnoreCase);
        bool Has(string name) => observed.ContainsKey(name);
        bool Ready(string[][] groups) => groups.All(group => group.Any(Has));
        var ranked = LearningTrackCatalog.Expanded.Where(track => Ready(track.Entry))
            .Where(t => t.Name is not ("Frontend React" or "Full Stack") || !(Has("React Native") || Has("Expo")) || Has("HTML") || Has("CSS") || Has("React Router"))
            .Select(track =>
            {
                var matched = track.Steps.Where(s => s.Signals.Any(Has)).ToList();
                var score = matched.Count * 10 + matched.Sum(s => s.Signals.Where(Has).Max(n => Math.Min(observed[n].RepositoryCount, 5)));
                var frontier = Array.FindLastIndex(track.Steps, s => s.Milestone && s.Signals.Any(Has)
                    && (!(s.Topic is "REST API" or "API REST" or "Web API") || Ready(s.Requires)));
                var candidates = track.Steps.Skip(frontier + 1).Where(s => !s.Signals.Any(Has) && Ready(s.Requires))
                    .Where(s => !(s.Topic == "POO" && Has("CRUD"))).ToList();
                var signals = track.Steps.SelectMany(s => s.Signals).Concat(candidates.SelectMany(s => s.Requires.SelectMany(g => g)))
                    .Distinct(StringComparer.OrdinalIgnoreCase).Where(Has).ToList();
                return new Ranked(track, Area(track.Name), score, candidates, signals);
            }).OrderByDescending(t => t.Score).ThenBy(t => t.Track.Name).ToList();

        // Select one backend stack, not one mandatory roadmap for each backend language.
        var selected = ranked.GroupBy(t => t.Area).Select(group => group.First()).ToList();
        // Full Stack only qualifies with both a frontend and an actual backend framework.
        var fullStack = selected.FirstOrDefault(t => t.Area == "Full Stack");
        if (fullStack is not null)
            selected = selected.Where(t => t.Area is "Frontend" or "Backend" or "Full Stack").ToList();
        else if (selected.Count > 0)
            selected = selected.Where(t => t.Score * 2 >= selected.Max(t => t.Score)).Take(3).ToList();

        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var output = new List<LearningTrackDto>();
        // Shared cross-stack steps belong to Full Stack instead of appearing in three cards.
        foreach (var item in selected.OrderBy(t => t.Area == "Full Stack" ? 0 : 1).ThenByDescending(t => t.Score).Take(3))
        {
            var next = new List<LearningStepDto>();
            foreach (var step in item.Candidates)
            {
                if (next.Count == 4) break;
                if (!used.Add(TopicKey(step.Topic))) continue;
                var summary = string.Join(", ", item.Signals.Select(name => $"{name} ({observed[name].RepositoryCount} repositório(s))"));
                next.Add(new(step.Topic,
                    $"Evidências consideradas: {summary}. {step.Topic} ainda não foi demonstrado na cobertura consultada; os pré-requisitos da sugestão têm sinais presentes. Ausência de evidência não significa falta de conhecimento.",
                    step.Practice, item.Signals));
            }
            output.Add(new(item.Track.Name, item.Area, item.Signals, next));
        }
        return output.OrderBy(t => t.Area switch { "Frontend" => 0, "Backend" => 1, "Full Stack" => 2, _ => 3 }).ToList();
    }

    public IReadOnlyList<RecommendationDto> Priorities(IReadOnlyList<LearningTrackDto> tracks, IReadOnlyList<SkillDto> skills)
    {
        var suggestions = new List<RecommendationDto>();
        for (var round = 0; round < 4 && suggestions.Count < 3; round++)
        foreach (var track in tracks)
        {
            if (round >= track.NextSteps.Count || suggestions.Count == 3) continue;
            var step = track.NextSteps[round];
            suggestions.Add(new(step.Topic, step.Reason, step.NextStep)
            {
                Track = track.Name,
                ConsideredEvidence = skills.Where(s => step.ConsideredSkills.Contains(s.Name, StringComparer.OrdinalIgnoreCase))
                    .Select(s => new RecommendationEvidenceDto(s.Name, s.RepositoryCount, s.Evidence)).ToList()
            });
        }
        return suggestions;
    }

    private static string Area(string name) => name.StartsWith("Backend") ? "Backend" : name.StartsWith("Frontend") ? "Frontend"
        : name.StartsWith("Mobile") ? "Mobile" : name.StartsWith("Dados") ? "Dados" : "Full Stack";
    // Equivalent SQL goals should not be repeated just because the roadmaps name them differently.
    private static string TopicKey(string topic) => topic is "SQL" or "Banco SQL" or "Persistência SQL" ? "SQL" : topic;
    private sealed record Ranked(LearningTrack Track, string Area, int Score, List<LearningStep> Candidates, List<string> Signals);
}
