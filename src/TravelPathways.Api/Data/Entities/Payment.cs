using TravelPathways.Api.Common;

namespace TravelPathways.Api.Data.Entities;

public sealed class Payment : TenantEntityBase
{
    public PaymentType PaymentType { get; set; }

    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }

    /// <summary>For PaymentType.Received: the client/lead.</summary>
    public Guid? LeadId { get; set; }
    public Lead? Lead { get; set; }

    /// <summary>For PaymentType.Received: optional package this payment is for.</summary>
    public Guid? PackageId { get; set; }
    public TourPackage? Package { get; set; }

    /// <summary>For PaymentType.Made: hotel or houseboat paid.</summary>
    public Guid? HotelId { get; set; }
    public Hotel? Hotel { get; set; }

    /// <summary>For PaymentType.Made: transport company paid.</summary>
    public Guid? TransportCompanyId { get; set; }
    public TransportCompany? TransportCompany { get; set; }

    /// <summary>Relative URL of uploaded screenshot, e.g. /uploads/tenants/.../payments/...</summary>
    public string? ScreenshotUrl { get; set; }
}
