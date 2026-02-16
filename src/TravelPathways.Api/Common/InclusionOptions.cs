namespace TravelPathways.Api.Common;

/// <summary>Fixed list of inclusion options. Selected = Inclusions in PDF; unselected = Exclusions. Must match frontend INCLUSION_OPTIONS.</summary>
public static class InclusionOptions
{
    public static readonly IReadOnlyList<(string Id, string Label)> All = new[]
    {
        ("shikara_1hr", "01 hour Shikara Ride; Complementary"),
        ("toll_tax_driver", "Toll tax diesel parking and driver allowances"),
        ("gondola_phase1", "Gondola tickets for phase 1"),
        ("gondola_phase2", "Gondola tickets for phase 2"),
        ("mugal_gardens", "Entry tickets of Srinagar Mughal Gardens"),
        ("air_train_coolie", "Air / Train Fare Coolie / Porter Charges / Camera charges"),
        ("donations_temples", "Donations at Temples"),
        ("extended_stay", "Extended stay or travelling due to any reason"),
        ("meals_not_specified", "Any meals other than those specified in Tour Cost"),
        ("personal_expenses", "Expenses of personal nature such as tips, telephone calls, laundry, liquor etc."),
        ("union_cabs_pony", "Union Cabs in Gulmarg, Sonmarg, Pahalgam and Pony rides"),
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
