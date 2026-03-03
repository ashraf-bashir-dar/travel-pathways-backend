using TravelPathways.Api.Common;

namespace TravelPathways.Api.Data.Entities;

/// <summary>Assignment of a confirmed package to a reservation manager. Tracks pending/completed and advance payment screenshots.</summary>
public sealed class Reservation : TenantEntityBase
{
    public Guid PackageId { get; set; }
    public TourPackage Package { get; set; } = null!;

    /// <summary>Reservation manager (user) assigned to this booking.</summary>
    public Guid AssignedToUserId { get; set; }
    public AppUser AssignedToUser { get; set; } = null!;

    /// <summary>User who assigned this package to reservation (Tour Manager). "Package confirmed by" in UI.</summary>
    public Guid? AssignedByUserId { get; set; }
    public AppUser? AssignedByUser { get; set; }

    public ReservationStatus Status { get; set; } = ReservationStatus.Pending;

    public string? Notes { get; set; }
    /// <summary>Final note by reservation manager when all hotels are reserved (optional).</summary>
    public string? FinalNotes { get; set; }

    public List<ReservationPaymentScreenshot> PaymentScreenshots { get; set; } = [];
    public List<ReservationDayCompletion> DayCompletions { get; set; } = [];
}

/// <summary>Per-day reservation done flag. Reservation person marks e.g. Day 1 hotel confirmed, Day 2 tomorrow.</summary>
public sealed class ReservationDayCompletion : EntityBase
{
    public Guid ReservationId { get; set; }
    public Reservation Reservation { get; set; } = null!;
    public int DayNumber { get; set; }
    public bool IsDone { get; set; }
    public DateTime? DoneAt { get; set; }
}

/// <summary>One advance payment receipt/screenshot for a reservation. Optional DayNumber for hotel-wise uploads.</summary>
public sealed class ReservationPaymentScreenshot : EntityBase
{
    public Guid ReservationId { get; set; }
    public Reservation Reservation { get; set; } = null!;

    /// <summary>Day number (1-based) this screenshot relates to. Null = general.</summary>
    public int? DayNumber { get; set; }

    /// <summary>Relative URL e.g. /uploads/tenants/.../reservations/...</summary>
    public string FileUrl { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
}
