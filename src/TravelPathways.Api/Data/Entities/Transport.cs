using TravelPathways.Api.Common;

namespace TravelPathways.Api.Data.Entities;

public sealed class TransportCompany : TenantEntityBase
{
    public string Name { get; set; } = string.Empty;
    public string ContactPerson { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Address { get; set; } = string.Empty;
    public string? GstNumber { get; set; }
    public string? PanNumber { get; set; }

    public string? AadharDocumentUrl { get; set; }
    public string? LicenceDocumentUrl { get; set; }

    public List<Vehicle> Vehicles { get; set; } = [];
}

public sealed class Vehicle : TenantEntityBase
{
    public Guid TransportCompanyId { get; set; }
    public TransportCompany TransportCompany { get; set; } = null!;

    public VehicleType VehicleType { get; set; } = VehicleType.Other;
    public string? VehicleModel { get; set; }
    public string? VehicleNumber { get; set; }
    public int SeatingCapacity { get; set; }

    // Stored as JSON string array
    public List<string> Features { get; set; } = [];
    public bool IsAcAvailable { get; set; }

    public List<VehiclePricing> Pricing { get; set; } = [];
}

public sealed class VehiclePricing : TenantEntityBase
{
    public Guid VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;

    public string PickupLocation { get; set; } = string.Empty;
    public string DropLocation { get; set; } = string.Empty;

    public decimal CostPrice { get; set; }
    public decimal SellingPrice { get; set; }

    public DateTime FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

