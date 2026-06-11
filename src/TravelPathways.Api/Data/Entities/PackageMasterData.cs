namespace TravelPathways.Api.Data.Entities;

/// <summary>Tenant-managed inclusion or exclusion option ticked on packages and shown on the PDF.</summary>
public sealed class PackageInclusionMaster : TenantEntityBase
{
    /// <summary>Stable code stored in TourPackage.InclusionIds / ExclusionIds (e.g. welcome_greeting).</summary>
    public string Code { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    /// <summary>True = inclusion option; false = exclusion option.</summary>
    public bool IsInclusion { get; set; } = true;
}

/// <summary>Tenant-managed pickup / drop location for packages.</summary>
public sealed class PackageLocationMaster : TenantEntityBase
{
    public string Name { get; set; } = string.Empty;

    public bool AllowPickup { get; set; } = true;

    public bool AllowDrop { get; set; } = true;

    public int SortOrder { get; set; }
}
