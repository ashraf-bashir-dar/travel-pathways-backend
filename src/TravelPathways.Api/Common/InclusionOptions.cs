using TravelPathways.Api.Localization;

namespace TravelPathways.Api.Common;

/// <summary>Fixed list of inclusion options. Selected = Inclusions in PDF; unselected = Exclusions. Must match frontend INCLUSION_OPTIONS.</summary>
public static class InclusionOptions
{
    public static readonly IReadOnlyList<(string Id, string Label)> All = new[]
    {
        ("welcome_greeting", "Welcome and greeting"),
        ("sightseeing_as_per_itinerary", "Sightseeing as per itinerary"),
        ("shikara_1hr", "1-hour Shikara ride (complimentary)"),
        ("accommodation_mentioned_hotels", "Accommodation in above mentioned hotels"),
        ("transportation", "Transportation"),
        ("meals_dinner_breakfast", "Meals (Dinner and Breakfast)"),
        ("gondola_phase1", "Gondola tickets (Phase 1)"),
        ("gondola_phase2", "Gondola tickets (Phase 2)"),
        ("mugal_gardens", "Entry tickets for Srinagar Mughal Gardens"),
        ("extended_stay", "Extended stay or travel due to any reason"),
        ("meals_not_specified", "Any meals not specified in the tour cost"),
        ("union_cabs_pony", "Local cabs in Gulmarg, Sonmarg, Pahalgam and pony rides")
    };

    public static IReadOnlyList<string> GetInclusionLabels(IEnumerable<string> selectedIds, string? language = null) =>
        GetLabels(selectedIds, included: true, language);

    public static IReadOnlyList<string> GetExclusionLabels(IEnumerable<string> selectedIds, string? language = null) =>
        GetLabels(selectedIds, included: false, language);

    private static IReadOnlyList<string> GetLabels(IEnumerable<string> selectedIds, bool included, string? language)
    {
        var set = new HashSet<string>(selectedIds ?? [], StringComparer.OrdinalIgnoreCase);
        var items = All.Where(x => set.Contains(x.Id) == included);
        return items.Select(x => ResolveLabel(x.Id, x.Label, language)).ToList();
    }

    private static string ResolveLabel(string id, string englishLabel, string? language)
    {
        var translated = InclusionTranslations.GetLabel(id, language);
        return translated ?? englishLabel;
    }
}
