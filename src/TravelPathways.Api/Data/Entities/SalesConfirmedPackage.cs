using TravelPathways.Api.Common;

namespace TravelPathways.Api.Data.Entities;

/// <summary>Sales module: manually logged confirmed package with profit tracking.</summary>
public sealed class SalesConfirmedPackage : TenantEntityBase
{
    public string ClientName { get; set; } = string.Empty;
    public string ClientPhone { get; set; } = string.Empty;
    public DateOnly ArrivalDate { get; set; }
    public DateOnly DepartureDate { get; set; }
    public decimal ExpectedProfit { get; set; }
    public decimal? ActualProfit { get; set; }
    /// <summary>Client quote (final amount) at confirmation time.</summary>
    public decimal TotalPackageCost { get; set; }
    public Guid? TourPackageId { get; set; }
    public TourPackage? TourPackage { get; set; }
    public bool IsCompleted { get; set; }
    public string? FinalReview { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? CompletedByUserId { get; set; }
    public DateOnly ConfirmationDate { get; set; }
    public SalesPackageSourceType SourceType { get; set; }
    public Guid? LeadId { get; set; }
    public Lead? Lead { get; set; }
    public SalesReferenceSourceType? ReferenceSourceType { get; set; }
    public string? ReferenceName { get; set; }
    public string? ReferenceContact { get; set; }
    public Guid RecordedByUserId { get; set; }
    public AppUser RecordedBy { get; set; } = null!;
}
