using TravelPathways.Api.Common;

namespace TravelPathways.Api.Data.Entities;

public sealed class Hotel : TenantEntityBase
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public Guid? AreaId { get; set; }
    public Area? Area { get; set; }
    public string Pincode { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public int? StarRating { get; set; }
    public bool IsHouseboat { get; set; }

    // Stored as JSON string array
    public List<string> Amenities { get; set; } = [];

    public string? Description { get; set; }
    public string? CheckInTime { get; set; }
    public string? CheckOutTime { get; set; }

    /// <summary>URLs of uploaded images (for package PDF, gallery, etc.).</summary>
    public List<string> ImageUrls { get; set; } = [];

    public List<AccommodationRate> Rates { get; set; } = [];
}

public sealed class AccommodationRate : TenantEntityBase
{
    public Guid HotelId { get; set; }
    public Hotel Hotel { get; set; } = null!;

    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    /// <summary>e.g. Standard, Deluxe, Suite, Luxury, Other.</summary>
    public string? RoomCategory { get; set; }
    public AccommodationMealPlan MealPlan { get; set; } = AccommodationMealPlan.MAP;

    public decimal CostPrice { get; set; }
    public decimal SellingPrice { get; set; }

    public decimal? ExtraBedCostPrice { get; set; }
    public decimal? ExtraBedSellingPrice { get; set; }
    public decimal? CnbCostPrice { get; set; }
    public decimal? CnbSellingPrice { get; set; }
}

