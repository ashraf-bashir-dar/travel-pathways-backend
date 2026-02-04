using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelPathways.Api.Common;
using TravelPathways.Api.Data;
using TravelPathways.Api.Data.Entities;
using TravelPathways.Api.MultiTenancy;
using TravelPathways.Api.Storage;

namespace TravelPathways.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/hotels")]
public sealed class HotelsController : TenantControllerBase
{
    private readonly AppDbContext _db;
    private readonly FileStorage _storage;

    public HotelsController(AppDbContext db, TenantContext tenant, FileStorage storage) : base(tenant)
    {
        _db = db;
        _storage = storage;
    }

    public sealed class AccommodationRateDto
    {
        public required string Id { get; init; }
        public required string HotelId { get; init; }
        public required DateTime FromDate { get; init; }
        public required DateTime ToDate { get; init; }
        public required AccommodationMealPlan MealPlan { get; init; }
        public required decimal CostPrice { get; init; }
        public required decimal SellingPrice { get; init; }
        public decimal? ExtraBedCostPrice { get; init; }
        public decimal? ExtraBedSellingPrice { get; init; }
        public decimal? CnbCostPrice { get; init; }
        public decimal? CnbSellingPrice { get; init; }
        public required string TenantId { get; init; }
        public required bool IsActive { get; init; }
        public required DateTime CreatedAt { get; init; }
        public required DateTime UpdatedAt { get; init; }
    }

    public sealed class HotelDto
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
        public required string Address { get; init; }
        public required string City { get; init; }
        public required string State { get; init; }
        public string? AreaId { get; init; }
        public string? AreaName { get; init; }
        public required string Pincode { get; init; }
        public required string PhoneNumber { get; init; }
        public string? Email { get; init; }
        public int? StarRating { get; init; }
        public required bool IsHouseboat { get; init; }
        public List<string>? Amenities { get; init; }
        public string? Description { get; init; }
        public string? CheckInTime { get; init; }
        public string? CheckOutTime { get; init; }
        public List<string>? ImageUrls { get; init; }
        public List<AccommodationRateDto>? Rates { get; init; }
        public required string TenantId { get; init; }
        public required bool IsActive { get; init; }
        public required DateTime CreatedAt { get; init; }
        public required DateTime UpdatedAt { get; init; }
    }

    public sealed class CreateAccommodationRateRequestDto
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public AccommodationMealPlan MealPlan { get; set; } = AccommodationMealPlan.MAP;
        public decimal CostPrice { get; set; }
        public decimal SellingPrice { get; set; }
        public decimal? ExtraBedCostPrice { get; set; }
        public decimal? ExtraBedSellingPrice { get; set; }
        public decimal? CnbCostPrice { get; set; }
        public decimal? CnbSellingPrice { get; set; }
    }

    public sealed class CreateHotelRequestDto
    {
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public Guid? AreaId { get; set; }
        public string Pincode { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Email { get; set; }
        public int? StarRating { get; set; }
        public bool IsHouseboat { get; set; }
        public List<string>? Amenities { get; set; }
        public string? Description { get; set; }
        public string? CheckInTime { get; set; }
        public string? CheckOutTime { get; set; }
        public List<string>? ImageUrls { get; set; }
        public List<CreateAccommodationRateRequestDto>? Rates { get; set; }
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PaginatedResponse<HotelDto>>>> GetHotels(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? searchTerm = null,
        [FromQuery] bool? isHouseboat = null,
        CancellationToken ct = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.Hotels.AsNoTracking()
            .Include(h => h.Rates)
            .Include(h => h.Area)
            .Where(h => h.TenantId == TenantId);

        if (isHouseboat is not null)
        {
            query = query.Where(h => h.IsHouseboat == isHouseboat.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var s = searchTerm.Trim().ToLower();
            query = query.Where(h => h.Name.ToLower().Contains(s) || h.City.ToLower().Contains(s) || h.Address.ToLower().Contains(s));
        }

        var total = await query.CountAsync(ct);
        var hotels = await query
            .OrderByDescending(h => h.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = hotels.Select(ToDto).ToList();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));

        return ApiResponse<PaginatedResponse<HotelDto>>.Ok(new PaginatedResponse<HotelDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalPages = totalPages
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<HotelDto>>> GetHotelById([FromRoute] Guid id, CancellationToken ct)
    {
        var hotel = await _db.Hotels.AsNoTracking()
            .Include(h => h.Rates)
            .Include(h => h.Area)
            .FirstOrDefaultAsync(h => h.Id == id && h.TenantId == TenantId, ct);
        if (hotel is null) return NotFound(ApiResponse<HotelDto>.Fail("Hotel not found"));
        return ApiResponse<HotelDto>.Ok(ToDto(hotel));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<HotelDto>>> CreateHotel([FromBody] CreateHotelRequestDto request, CancellationToken ct)
    {
        var hotel = new Hotel
        {
            TenantId = TenantId,
            Name = request.Name.Trim(),
            Address = request.Address.Trim(),
            City = request.City.Trim(),
            State = request.State.Trim(),
            AreaId = request.AreaId,
            Pincode = request.Pincode.Trim(),
            PhoneNumber = request.PhoneNumber.Trim(),
            Email = request.Email?.Trim(),
            StarRating = request.StarRating,
            IsHouseboat = request.IsHouseboat,
            Amenities = request.Amenities ?? [],
            Description = request.Description?.Trim(),
            CheckInTime = request.CheckInTime?.Trim(),
            CheckOutTime = request.CheckOutTime?.Trim(),
            ImageUrls = request.ImageUrls ?? []
        };

        _db.Hotels.Add(hotel);
        await _db.SaveChangesAsync(ct);

        if (request.Rates is { Count: > 0 })
        {
            foreach (var r in request.Rates)
            {
                _db.AccommodationRates.Add(new AccommodationRate
                {
                    TenantId = TenantId,
                    HotelId = hotel.Id,
                    FromDate = r.FromDate,
                    ToDate = r.ToDate,
                    MealPlan = r.MealPlan,
                    CostPrice = r.CostPrice,
                    SellingPrice = r.SellingPrice,
                    ExtraBedCostPrice = r.ExtraBedCostPrice,
                    ExtraBedSellingPrice = r.ExtraBedSellingPrice,
                    CnbCostPrice = r.CnbCostPrice,
                    CnbSellingPrice = r.CnbSellingPrice
                });
            }
            await _db.SaveChangesAsync(ct);
        }

        var created = await _db.Hotels.AsNoTracking().Include(h => h.Rates).Include(h => h.Area).FirstAsync(h => h.Id == hotel.Id, ct);
        return CreatedAtAction(nameof(GetHotelById), new { id = hotel.Id }, ApiResponse<HotelDto>.Ok(ToDto(created)));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<HotelDto>>> UpdateHotel([FromRoute] Guid id, [FromBody] CreateHotelRequestDto request, CancellationToken ct)
    {
        var hotel = await _db.Hotels.Include(h => h.Rates).Include(h => h.Area).FirstOrDefaultAsync(h => h.Id == id && h.TenantId == TenantId, ct);
        if (hotel is null) return NotFound(ApiResponse<HotelDto>.Fail("Hotel not found"));

        hotel.Name = request.Name.Trim();
        hotel.Address = request.Address.Trim();
        hotel.City = request.City.Trim();
        hotel.State = request.State.Trim();
        hotel.AreaId = request.AreaId;
        hotel.Pincode = request.Pincode.Trim();
        hotel.PhoneNumber = request.PhoneNumber.Trim();
        hotel.Email = request.Email?.Trim();
        hotel.StarRating = request.StarRating;
        hotel.IsHouseboat = request.IsHouseboat;
        hotel.Amenities = request.Amenities ?? [];
        hotel.Description = request.Description?.Trim();
        hotel.CheckInTime = request.CheckInTime?.Trim();
        hotel.CheckOutTime = request.CheckOutTime?.Trim();
        if (request.ImageUrls is not null)
            hotel.ImageUrls = request.ImageUrls;

        // If rates are provided, replace them; otherwise keep existing.
        if (request.Rates is not null)
        {
            var existing = await _db.AccommodationRates.Where(r => r.HotelId == hotel.Id && r.TenantId == TenantId).ToListAsync(ct);
            _db.AccommodationRates.RemoveRange(existing);

            foreach (var r in request.Rates)
            {
                _db.AccommodationRates.Add(new AccommodationRate
                {
                    TenantId = TenantId,
                    HotelId = hotel.Id,
                    FromDate = r.FromDate,
                    ToDate = r.ToDate,
                    MealPlan = r.MealPlan,
                    CostPrice = r.CostPrice,
                    SellingPrice = r.SellingPrice,
                    ExtraBedCostPrice = r.ExtraBedCostPrice,
                    ExtraBedSellingPrice = r.ExtraBedSellingPrice,
                    CnbCostPrice = r.CnbCostPrice,
                    CnbSellingPrice = r.CnbSellingPrice
                });
            }
        }

        await _db.SaveChangesAsync(ct);

        var updated = await _db.Hotels.AsNoTracking().Include(h => h.Rates).Include(h => h.Area).FirstAsync(h => h.Id == hotel.Id, ct);
        return ApiResponse<HotelDto>.Ok(ToDto(updated));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteHotel([FromRoute] Guid id, CancellationToken ct)
    {
        var hotel = await _db.Hotels.FirstOrDefaultAsync(h => h.Id == id && h.TenantId == TenantId, ct);
        if (hotel is null) return NotFound(ApiResponse<object>.Fail("Hotel not found"));
        _db.Hotels.Remove(hotel);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(new { });
    }

    /// <summary>Upload one or more images for a hotel/houseboat. Returns the list of image URLs (including newly added). Form key: "files".</summary>
    [HttpPost("{id:guid}/images")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ApiResponse<List<string>>>> UploadImages([FromRoute] Guid id, [FromForm(Name = "files")] IFormFile[]? files, CancellationToken ct)
    {
        var hotel = await _db.Hotels.FirstOrDefaultAsync(h => h.Id == id && h.TenantId == TenantId, ct);
        if (hotel is null) return NotFound(ApiResponse<List<string>>.Fail("Hotel not found"));

        hotel.ImageUrls ??= [];
        foreach (var file in files ?? [])
        {
            if (file.Length == 0) continue;
            var url = await _storage.SaveHotelImageAsync(TenantId, id, file, ct);
            hotel.ImageUrls.Add(url);
        }
        await _db.SaveChangesAsync(ct);

        return ApiResponse<List<string>>.Ok(hotel.ImageUrls);
    }

    /// <summary>Remove one image URL from a hotel. Pass the full URL (e.g. from ImageUrls list).</summary>
    [HttpDelete("{id:guid}/images")]
    public async Task<ActionResult<ApiResponse<List<string>>>> RemoveImage([FromRoute] Guid id, [FromQuery] string url, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url)) return BadRequest(ApiResponse<List<string>>.Fail("url is required"));
        var hotel = await _db.Hotels.FirstOrDefaultAsync(h => h.Id == id && h.TenantId == TenantId, ct);
        if (hotel is null) return NotFound(ApiResponse<List<string>>.Fail("Hotel not found"));

        hotel.ImageUrls ??= [];
        hotel.ImageUrls.RemoveAll(u => string.Equals(u, url.Trim(), StringComparison.OrdinalIgnoreCase));
        await _db.SaveChangesAsync(ct);

        return ApiResponse<List<string>>.Ok(hotel.ImageUrls);
    }

    [HttpGet("{hotelId:guid}/rooms")]
    public ActionResult<ApiResponse<List<object>>> GetHotelRooms([FromRoute] Guid hotelId)
    {
        return ApiResponse<List<object>>.Ok([]);
    }

    private static HotelDto ToDto(Hotel h) =>
        new()
        {
            Id = h.Id.ToString("D"),
            Name = h.Name,
            Address = h.Address,
            City = h.City,
            State = h.State,
            AreaId = h.AreaId?.ToString("D"),
            AreaName = h.Area?.Name,
            Pincode = h.Pincode,
            PhoneNumber = h.PhoneNumber,
            Email = h.Email,
            StarRating = h.StarRating,
            IsHouseboat = h.IsHouseboat,
            Amenities = h.Amenities ?? [],
            Description = h.Description,
            CheckInTime = h.CheckInTime,
            CheckOutTime = h.CheckOutTime,
            ImageUrls = h.ImageUrls ?? [],
            Rates = (h.Rates ?? []).Select(r => new AccommodationRateDto
            {
                Id = r.Id.ToString("D"),
                HotelId = r.HotelId.ToString("D"),
                FromDate = r.FromDate,
                ToDate = r.ToDate,
                MealPlan = r.MealPlan,
                CostPrice = r.CostPrice,
                SellingPrice = r.SellingPrice,
                ExtraBedCostPrice = r.ExtraBedCostPrice,
                ExtraBedSellingPrice = r.ExtraBedSellingPrice,
                CnbCostPrice = r.CnbCostPrice,
                CnbSellingPrice = r.CnbSellingPrice,
                TenantId = r.TenantId.ToString("D"),
                IsActive = r.IsActive,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            }).ToList(),
            TenantId = h.TenantId.ToString("D"),
            IsActive = h.IsActive,
            CreatedAt = h.CreatedAt,
            UpdatedAt = h.UpdatedAt
        };
}

