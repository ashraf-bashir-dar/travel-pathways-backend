namespace TravelPathways.Api.Data.Entities;

/// <summary>Reusable driver master record (company or freelance).</summary>
public sealed class Driver : TenantEntityBase
{
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }

    /// <summary>Optional — null for freelance drivers.</summary>
    public Guid? TransportCompanyId { get; set; }
    public TransportCompany? TransportCompany { get; set; }

    public string? LicenceNumber { get; set; }
    public string? AadharLastFour { get; set; }
    public string? LicenceDocumentUrl { get; set; }
    public string? AadharDocumentUrl { get; set; }
    public string? Notes { get; set; }

    public string? VehicleNumber { get; set; }
    public string? VehicleModel { get; set; }
    public string? VehicleImageUrl { get; set; }

    public List<PackageDriverAssignment> Assignments { get; set; } = [];
}

/// <summary>Driver assigned to a confirmed package / reservation for one tour.</summary>
public sealed class PackageDriverAssignment : TenantEntityBase
{
    public Guid PackageId { get; set; }
    public TourPackage Package { get; set; } = null!;

    public Guid ReservationId { get; set; }
    public Reservation Reservation { get; set; } = null!;

    public Guid DriverId { get; set; }
    public Driver Driver { get; set; } = null!;

    /// <summary>Transport company that supplied this driver for the tour (optional for freelance).</summary>
    public Guid? TransportCompanyId { get; set; }
    public TransportCompany? TransportCompany { get; set; }

    public string VehicleNumber { get; set; } = string.Empty;
    public string? VehicleModel { get; set; }

    /// <summary>Vehicle photo for this tour (assignment-level only).</summary>
    public string? VehicleImageUrl { get; set; }

    public Guid? AssignedByUserId { get; set; }
    public AppUser? AssignedByUser { get; set; }

    /// <summary>1–5 service rating; set by admin when tour completes.</summary>
    public int? ServiceRating { get; set; }
    public string? ServiceNotes { get; set; }
    public Guid? RatedByUserId { get; set; }
    public AppUser? RatedByUser { get; set; }
    public DateTime? RatedAtUtc { get; set; }
}
