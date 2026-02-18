namespace TravelPathways.Api.Common;

/// <summary>Fixed list of inclusion options. Selected = Inclusions in PDF; unselected = Exclusions. Must match frontend INCLUSION_OPTIONS.</summary>
public static class InclusionOptions
{
    public static readonly IReadOnlyList<(string Id, string Label)> All = new[]
    {
        ("shikara_1hr", "1-hour Shikara ride (complimentary)"),
        ("toll_tax_driver", "Toll tax, diesel, parking and driver allowances"),
        ("gondola_phase1", "Gondola tickets (Phase 1)"),
        ("gondola_phase2", "Gondola tickets (Phase 2)"),
        ("mugal_gardens", "Entry tickets for Srinagar Mughal Gardens"),
        ("air_train_coolie", "Air or train fare, coolie/porter charges and camera charges"),
        ("donations_temples", "Donations at temples"),
        ("extended_stay", "Extended stay or travel due to any reason"),
        ("meals_not_specified", "Any meals not specified in the tour cost"),
        ("personal_expenses", "Personal expenses (e.g. tips, telephone, laundry, liquor)"),
        ("union_cabs_pony", "Local cabs in Gulmarg, Sonmarg, Pahalgam and pony rides"),
        ("health_insurance", "Health insurance")
    };

    public static IReadOnlyList<string> GetInclusionLabels(IEnumerable<string> selectedIds)
    {
        var set = new HashSet<string>(selectedIds ?? [], StringComparer.OrdinalIgnoreCase);
        return All.Where(x => set.Contains(x.Id)).Select(x => x.Label).ToList();
    }

    public static IReadOnlyList<string> GetExclusionLabels(IEnumerable<string> selectedIds)
    {
        var set = new HashSet<string>(selectedIds ?? [], StringComparer.OrdinalIgnoreCase);
        return All.Where(x => !set.Contains(x.Id)).Select(x => x.Label).ToList();
    }
}
