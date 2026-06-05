using TravelPathways.Api.Common;

namespace TravelPathways.Api.Data.Entities;

/// <summary>History entry when a package is created or its details are updated (client-facing proposal revision).</summary>
public sealed class PackageLog : TenantEntityBase
{
    public Guid LeadId { get; set; }
    public Lead? Lead { get; set; }

    public Guid PackageId { get; set; }
    public TourPackage? Package { get; set; }

    public PackageLogAction Action { get; set; }

    /// <summary>Snapshot at log time.</summary>
    public string PackageName { get; set; } = string.Empty;

    /// <summary>Snapshot: TotalAmount - Discount at log time.</summary>
    public decimal FinalAmount { get; set; }

    /// <summary>Agency margin (INR) at log time.</summary>
    public decimal MarginAmount { get; set; }

    public PackageStatus Status { get; set; }

    public Guid? ChangedByUserId { get; set; }
    public AppUser? ChangedByUser { get; set; }

    public string ChangedByDisplayName { get; set; } = string.Empty;

    /// <summary>Full package form snapshot (JSON, camelCase).</summary>
    public string SnapshotJson { get; set; } = "{}";
}
