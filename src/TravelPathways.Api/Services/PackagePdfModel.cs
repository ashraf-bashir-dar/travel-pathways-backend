using TravelPathways.Api.Localization;

namespace TravelPathways.Api.Services;

/// <summary>Data required to render the package PDF (HTML template input).</summary>
public sealed class PackagePdfModel
{
    public PdfLocalizedStrings Labels { get; init; } = PdfLocalizedStrings.English();
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
    /// <summary>Selected vehicle for the package (e.g. "Innova - AC").</summary>
    public string? VehicleName { get; init; }
    /// <summary>Pickup → drop route line for transport section.</summary>
    public string? TransportRoute { get; init; }
    public int NumberOfAdults { get; init; }
    public int NumberOfChildren { get; init; }
    public string MealPlanLabel { get; init; } = "–";
    public int FirstDayRooms { get; init; } = 1;
    /// <summary>Peak extra beds on any hotel night (max per day where a hotel is assigned). Better for PDF than summing nightly repeats.</summary>
    public int TotalExtraBeds { get; init; }
    /// <summary>Peak CNB (Child No Bed) on any hotel night.</summary>
    public int TotalCnbCount { get; init; }
    public required string TotalAmount { get; init; }

    /// <summary>Same formatted value as <see cref="TotalAmount"/> (pre-discount package quote from DB).</summary>
    public required string TotalPackagePrice { get; init; }

    /// <summary>Formatted margin for PDF templates (e.g. "Rs. 5,000" or "–").</summary>
    public required string MarginAmountDisplay { get; init; }

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
    /// <summary>Tenant/agency logo URL (absolute or data URL for PDF).</summary>
    public string? AgencyLogoUrl { get; init; }
    /// <summary>Managing director name (from tenant Contact Person).</summary>
    public string? ManagingDirectorName { get; init; }
    /// <summary>Optional sales head line (defaults to contact person when set).</summary>
    public string? SalesHeadName { get; init; }
    /// <summary>HTML for registered office address block (tenant address or localized default).</summary>
    public string? RegisteredOfficeAddressHtml { get; init; }
    /// <summary>Derived from tenant email domain when no website field exists.</summary>
    public string? AgencyWebsite { get; init; }
    /// <summary>JK tourism / agency registration licence (e.g. JKTA00004788).</summary>
    public string? AgencyLicenseNumber { get; init; }
    public string GeneratedDate { get; init; } = "";
    /// <summary>Bank accounts to show for payment (from tenant).</summary>
    public List<BankAccountItem> BankAccounts { get; init; } = [];
    /// <summary>QR codes to show (label + image URL).</summary>
    public List<QrCodeItem> QrCodes { get; init; } = [];

    /// <summary>Per-tenant PDF overrides. When null/empty, generator uses built-in default.</summary>
    public string? PrimaryColor { get; init; }
    public string? SecondaryColor { get; init; }
    public string? CoverTitle { get; init; }
    /// <summary>Template key selected for tenant PDF rendering.</summary>
    public string? TemplateKey { get; init; }
    /// <summary>Optional custom HTML template body loaded from template library.</summary>
    public string? CustomHtmlTemplate { get; init; }
    public bool? ShowBankDetails { get; init; }
    public bool? ShowQrCodes { get; init; }
    public List<string> TermsAndConditions { get; init; } = [];
    public List<string> CancellationPolicy { get; init; } = [];
    public List<string> SupplementCosts { get; init; } = [];
}

public sealed class BankAccountItem
{
    public string AccountHolderName { get; init; } = "";
    public string BankName { get; init; } = "";
    public string AccountNumber { get; init; } = "";
    public string IFSC { get; init; } = "";
    public string? Branch { get; init; }
}

public sealed class QrCodeItem
{
    public string Label { get; init; } = "";
    public string ImageUrl { get; init; } = "";
}

public sealed class DayItem
{
    public int DayNumber { get; init; }
    /// <summary>Display date for this day (e.g. 12 May 2026).</summary>
    public string DateLabel { get; init; } = "";
    /// <summary>Hotel/houseboat name mapped to this day itinerary row.</summary>
    public string? HotelName { get; init; }
    /// <summary>Hotel area/location mapped to this day itinerary row.</summary>
    public string? HotelLocation { get; init; }
    /// <summary>Representative image URL for this day (usually hotel's first image).</summary>
    public string? DayImageUrl { get; init; }
    public string Title { get; init; } = "";
    /// <summary>Itinerary template title (dropdown label), shown after date in itinerary overview.</summary>
    public string TemplateTitle { get; init; } = "";
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
    /// <summary>Max extra beds on any single night at this hotel (avoids summing the same nightly count across days).</summary>
    public int ExtraBedCount { get; init; }
    /// <summary>Max CNB on any single night at this hotel.</summary>
    public int CnbCount { get; init; }
    public bool IsHouseboat { get; init; }
    public List<string> Amenities { get; init; } = [];
    public List<string> ImageUrls { get; init; } = [];
}
