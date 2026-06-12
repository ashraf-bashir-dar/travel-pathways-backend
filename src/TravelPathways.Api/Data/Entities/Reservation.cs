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
    public bool IsLocked { get; set; }

    public string? Notes { get; set; }
    /// <summary>Final note by reservation manager when all hotels are reserved (optional).</summary>
    public string? FinalNotes { get; set; }

    public List<ReservationPaymentScreenshot> PaymentScreenshots { get; set; } = [];
    public List<ReservationDayCompletion> DayCompletions { get; set; } = [];
    public List<ReservationHotelBooking> HotelBookings { get; set; } = [];
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

public sealed class ReservationHotelBooking : TenantEntityBase
{
    public Guid ReservationId { get; set; }
    public Reservation Reservation { get; set; } = null!;

    public int DayNumber { get; set; }
    public DateTime BookingDate { get; set; }
    public DateTime? CheckInDate { get; set; }
    public DateTime? CheckOutDate { get; set; }

    public Guid? HotelId { get; set; }
    public Hotel? Hotel { get; set; }
    public string HotelName { get; set; } = string.Empty;
    public bool IsHouseboat { get; set; }
    public string? RoomType { get; set; }
    public int NumberOfRooms { get; set; }
    public int ExtraBedCount { get; set; }
    public int CnbCount { get; set; }
    public int NumberOfPersons { get; set; }

    public decimal RatePerNight { get; set; }
    public decimal ExtraBedRate { get; set; }
    public decimal CnbRate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AdvancePaid { get; set; }
    public decimal BalanceAmount { get; set; }
    public ReservationHotelBookingStatus Status { get; set; } = ReservationHotelBookingStatus.Pending;
    public bool IsLocked { get; set; }
    public ReservationHotelBookingCancellationReason? CancellationReason { get; set; }
    public string? CancellationReasonDetail { get; set; }
    public string? ConfirmationNumber { get; set; }
    public string? Notes { get; set; }

    public List<ReservationHotelBookingDocument> Documents { get; set; } = [];
}

public sealed class ReservationHotelBookingDocument : EntityBase
{
    public Guid ReservationHotelBookingId { get; set; }
    public ReservationHotelBooking ReservationHotelBooking { get; set; } = null!;

    public ReservationHotelBookingDocumentType Type { get; set; } = ReservationHotelBookingDocumentType.PaymentProof;
    public decimal? Amount { get; set; }
    public DateTime? PaymentDate { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
}
