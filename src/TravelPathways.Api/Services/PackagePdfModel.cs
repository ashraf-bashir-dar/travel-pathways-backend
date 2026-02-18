namespace TravelPathways.Api.Services;

/// <summary>Data required to render the package PDF (HTML template input).</summary>
public sealed class PackagePdfModel
{
    public required string PackageName { get; init; }
    public required string ClientName { get; init; }
    public string? ClientPhone { get; init; }
    public string? ClientEmail { get; init; }
    public string? ClientAddress { get; init; }
    public required string StartDate { get; init; }
    public required string EndDate { get; init; }
    public required string DaysLabel { get; init; }
    public string? PickUpLocation { get; init; }
    public string? DropLocation { get; init; }
    public int NumberOfAdults { get; init; }
    public int NumberOfChildren { get; init; }
    public string MealPlanLabel { get; init; } = "–";
    public int FirstDayRooms { get; init; } = 1;
    /// <summary>Total extra beds across all days. Shown in Package information when > 0.</summary>
    public int TotalExtraBeds { get; init; }
    /// <summary>Total CNB (Child No Bed) across all days. Shown in Package information when > 0.</summary>
    public int TotalCnbCount { get; init; }
    public required string TotalAmount { get; init; }
    public required string Discount { get; init; }
    public required string FinalAmount { get; init; }
    /// <summary>Per person cost = Final amount / (Adults + Children).</summary>
    public string? PerPersonAmount { get; init; }
    public required string AdvanceAmount { get; init; }
    public required string BalanceAmount { get; init; }
    public List<DayItem> Days { get; init; } = [];
    public List<HotelItem> Hotels { get; init; } = [];
    /// <summary>First 4 images from hotels for cover page right panel.</summary>
    public List<string> CoverImageUrls { get; init; } = [];
    public List<string> InclusionLabels { get; init; } = [];
    public List<string> ExclusionLabels { get; init; } = [];
    public string? AgencyName { get; init; }
    public string? AgencyPhone { get; init; }
    public string? AgencyEmail { get; init; }
    /// <summary>Managing director name (from tenant Contact Person).</summary>
    public string? ManagingDirectorName { get; init; }
    public string GeneratedDate { get; init; } = "";
}

public sealed class DayItem
{
    public int DayNumber { get; init; }
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    /// <summary>Extra beds for this day. Shown in PDF when > 0.</summary>
    public int ExtraBedCount { get; init; }
    /// <summary>CNB (Child No Bed) for this day. Shown in PDF when > 0.</summary>
    public int CnbCount { get; init; }
}

public sealed class HotelItem
{
    public string Name { get; init; } = "";
    public string Location { get; init; } = "";
    public int StarRating { get; init; }
    public string MealPlan { get; init; } = "";
    public int Nights { get; init; }
    public bool IsHouseboat { get; init; }
    public List<string> Amenities { get; init; } = [];
    public List<string> ImageUrls { get; init; } = [];
}
