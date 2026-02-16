using TravelPathways.Api.Common;

namespace TravelPathways.Api.Data.Entities;

public sealed class TourPackage : TenantEntityBase
{
    public Guid? LeadId { get; set; }
    public Lead? Lead { get; set; }

    public string ClientName { get; set; } = string.Empty;
    public string ClientPhone { get; set; } = string.Empty;
    public string? ClientEmail { get; set; }
    public string? ClientCity { get; set; }
    public string? ClientState { get; set; }

    public string ClientPickupLocation { get; set; } = string.Empty;
    public string ClientDropLocation { get; set; } = string.Empty;

    public string PackageName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int NumberOfDays { get; set; }

    public int NumberOfAdults { get; set; }
    public int NumberOfChildren { get; set; }

    public Guid? VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }

    public decimal TotalAmount { get; set; }
    /// <summary>Discount amount (e.g. in INR). Final package cost = TotalAmount - Discount.</summary>
    public decimal Discount { get; set; }
    public decimal AdvanceAmount { get; set; }
    public decimal BalanceAmount { get; set; }

    public PackageStatus Status { get; set; } = PackageStatus.New;

    /// <summary>Ids of inclusion options that are selected (shown as Inclusions in PDF). Unselected appear as Exclusions.</summary>
    public List<string> InclusionIds { get; set; } = [];

    public List<DayItinerary> DayWiseItinerary { get; set; } = [];

    public string CreatedBy { get; set; } = string.Empty;
}

public sealed class DayItinerary : TenantEntityBase
{
    public Guid PackageId { get; set; }
    public TourPackage Package { get; set; } = null!;

    public int DayNumber { get; set; }
    public DateTime Date { get; set; }

    public Guid? HotelId { get; set; }
    public Hotel? Hotel { get; set; }

    public string? RoomType { get; set; }
    public int NumberOfRooms { get; set; }
    public string? CheckInTime { get; set; }
    public string? CheckOutTime { get; set; }
    public AccommodationMealPlan MealPlan { get; set; } = AccommodationMealPlan.MAP;
    public int? ExtraBedCount { get; set; }
    public int? CnbCount { get; set; }

    // Stored as JSON string array
    public List<string> Activities { get; set; } = [];

    // Stored as JSON string array (MealType values from frontend)
    public List<string> Meals { get; set; } = [];

    public string? Notes { get; set; }

    /// <summary>Optional link to DestinationMaster (day description template) for PDF heading "Day 01: Template Name".</summary>
    public Guid? ItineraryTemplateId { get; set; }
    public ItineraryTemplate? ItineraryTemplate { get; set; }

    public decimal HotelCost { get; set; }
}

