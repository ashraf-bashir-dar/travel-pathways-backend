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
