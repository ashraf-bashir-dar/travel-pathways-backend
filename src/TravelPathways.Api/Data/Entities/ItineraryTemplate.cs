namespace TravelPathways.Api.Data.Entities;

/// <summary>
/// Reusable day description for package itineraries (e.g. "Srinagar – Gulmarg", "Pick from Srinagar hotel – Sonmarg").
/// Select one per day in the package form to fill the day description without retyping.
/// </summary>
public sealed class ItineraryTemplate : TenantEntityBase
{
    /// <summary>Short title shown in the dropdown (e.g. "Srinagar – Gulmarg").</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Full description text applied to the day when selected. Used in PDF and notes.</summary>
    public string Description { get; set; } = string.Empty;
}
