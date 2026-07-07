using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
    private readonly IConfiguration _configuration;

    public HotelsController(AppDbContext db, TenantContext tenant, FileStorage storage, IConfiguration configuration)
        : base(tenant)
    {
        _db = db;
        _storage = storage;
        _configuration = configuration;
    }

    private string? ResolvePublicApiBase() => PublicApiBaseResolver.Resolve(_configuration, HttpContext);

    /// <summary>DB keeps /uploads/... relative paths; API JSON returns absolute URLs for browsers when a public base URL is configured.</summary>
    private List<string> NormalizeImageUrlsForClient(IReadOnlyList<string>? urls)
    {
        if (urls is null || urls.Count == 0)
            return [];

        var baseUri = ResolvePublicApiBase();
        if (string.IsNullOrEmpty(baseUri))
        {
            return urls
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .Select(static u => u.Trim())
                .ToList();
        }

        List<string> result = [];
        foreach (var u in urls)
        {
            if (string.IsNullOrWhiteSpace(u)) continue;
            var s = u.Trim();
            if (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                result.Add(s);
                continue;
            }

            var path = s.StartsWith('/') ? s : "/" + s;
            result.Add($"{baseUri}{path}");
        }

        return result;
    }

    /// <summary>Persist paths as /uploads/... even if the SPA round-tripped absolute URLs from GET.</summary>
    private static List<string> NormalizeImageUrlsForStorage(IReadOnlyList<string>? urls)
    {
        if (urls is null || urls.Count == 0)
            return [];

        List<string> result = [];
        foreach (var u in urls)
        {
            if (string.IsNullOrWhiteSpace(u)) continue;
            var s = u.Trim();
            if (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    s = new Uri(s).AbsolutePath;
                }
                catch
                {
                    continue;
                }
            }

            if (!s.StartsWith('/'))
                s = "/" + s;
            result.Add(s);
        }

        return result;
    }

    public sealed class AccommodationRateDto
    {
        public required string Id { get; init; }
        public required string HotelId { get; init; }
        public required DateTime FromDate { get; init; }
        public required DateTime ToDate { get; init; }
        public string? RoomCategory { get; init; }
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

    public sealed class HotelLookupDto
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
        public required string City { get; init; }
        public required string State { get; init; }
        public string? AreaName { get; init; }
        public required bool IsHouseboat { get; init; }
        public required bool IsActive { get; init; }
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
        public string? RoomCategory { get; set; }
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
        [FromQuery] int? starRating = null,
        [FromQuery] Guid? areaId = null,
        [FromQuery] decimal? minPrice = null,
        [FromQuery] decimal? maxPrice = null,
        CancellationToken ct = default)
    {
        var viewModule = isHouseboat == true
            ? AppModuleKey.Houseboats
            : AppModuleKey.Hotels;
        if (await DenyUnlessModuleActionAsync(_db, viewModule, ModuleAction.View, ct) is { } viewDenied)
            return viewDenied;

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
            var pattern = PostgresSearch.ToContainsPattern(searchTerm);
            query = query.Where(h =>
                EF.Functions.ILike(h.Name, pattern, "\\") ||
                EF.Functions.ILike(h.City, pattern, "\\") ||
                EF.Functions.ILike(h.Address, pattern, "\\") ||
                (h.Area != null && EF.Functions.ILike(h.Area.Name, pattern, "\\")));
        }

        if (starRating is >= 1 and <= 5)
        {
            query = query.Where(h => h.StarRating != null && h.StarRating == starRating.Value);
        }

        if (areaId is not null && areaId != Guid.Empty)
        {
            query = query.Where(h => h.AreaId == areaId);
        }

        // Room cost filter: include hotel if ANY rate (any category / date range) matches.
        // Max only → at least one rate with cost <= max. Min only → at least one rate with cost >= min.
        // Both → at least one rate with min <= cost <= max.
        if (minPrice is > 0 || maxPrice is > 0)
        {
            var min = minPrice is > 0 ? minPrice.Value : (decimal?)null;
            var max = maxPrice is > 0 ? maxPrice.Value : (decimal?)null;
            query = query.Where(h =>
                _db.AccommodationRates.Any(r =>
                    r.HotelId == h.Id &&
                    (min == null || r.CostPrice >= min) &&
                    (max == null || r.CostPrice <= max)));
        }

        var total = await query.CountAsync(ct);
        var hotels = await query
            .OrderByDescending(h => h.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = hotels.Select(ToHotelDto).ToList();
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

    /// <summary>Lightweight hotel list for dropdowns (no rates or images).</summary>
    [HttpGet("lookup")]
    public async Task<ActionResult<ApiResponse<PaginatedResponse<HotelLookupDto>>>> GetHotelLookup(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 200,
        [FromQuery] string? searchTerm = null,
        [FromQuery] bool? isHouseboat = null,
        CancellationToken ct = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 500);

        var query = _db.Hotels.AsNoTracking()
            .Include(h => h.Area)
            .Where(h => h.TenantId == TenantId && h.IsActive);

        if (isHouseboat is not null)
            query = query.Where(h => h.IsHouseboat == isHouseboat.Value);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var pattern = PostgresSearch.ToContainsPattern(searchTerm);
            query = query.Where(h =>
                EF.Functions.ILike(h.Name, pattern, "\\") ||
                EF.Functions.ILike(h.City, pattern, "\\") ||
                (h.Area != null && EF.Functions.ILike(h.Area.Name, pattern, "\\")));
        }

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderBy(h => h.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(h => new HotelLookupDto
            {
                Id = h.Id.ToString("D"),
                Name = h.Name,
                City = h.City,
                State = h.State,
                AreaName = h.Area != null ? h.Area.Name : null,
                IsHouseboat = h.IsHouseboat,
                IsActive = h.IsActive
            })
            .ToListAsync(ct);

        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        return ApiResponse<PaginatedResponse<HotelLookupDto>>.Ok(new PaginatedResponse<HotelLookupDto>
        {
            Items = rows,
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
        if (await DenyUnlessModuleActionAsync(_db, ModuleForHotel(hotel.IsHouseboat), ModuleAction.View, ct) is { } viewDenied)
            return viewDenied;
        return ApiResponse<HotelDto>.Ok(ToHotelDto(hotel));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<HotelDto>>> CreateHotel([FromBody] CreateHotelRequestDto request, CancellationToken ct)
    {
        if (await DenyUnlessModuleActionAsync(_db, ModuleForHotel(request.IsHouseboat), ModuleAction.Create, ct) is { } createDenied)
            return createDenied;

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
            ImageUrls = NormalizeImageUrlsForStorage(request.ImageUrls ?? [])
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
                    FromDate = NormalizeUtc(r.FromDate),
                    ToDate = NormalizeUtc(r.ToDate),
                    RoomCategory = r.RoomCategory?.Trim(),
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
        return CreatedAtAction(nameof(GetHotelById), new { id = hotel.Id }, ApiResponse<HotelDto>.Ok(ToHotelDto(created)));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<HotelDto>>> UpdateHotel([FromRoute] Guid id, [FromBody] CreateHotelRequestDto request, CancellationToken ct)
    {
        var hotel = await _db.Hotels.Include(h => h.Rates).Include(h => h.Area).FirstOrDefaultAsync(h => h.Id == id && h.TenantId == TenantId, ct);
        if (hotel is null) return NotFound(ApiResponse<HotelDto>.Fail("Hotel not found"));
        if (await DenyUnlessModuleActionAsync(_db, ModuleForHotel(hotel.IsHouseboat), ModuleAction.Edit, ct) is { } editDenied)
            return editDenied;

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
            hotel.ImageUrls = NormalizeImageUrlsForStorage(request.ImageUrls);

        // If rates are provided, replace them; otherwise keep existing.
        if (request.Rates is not null)
        {
            hotel.Rates.Clear();
            var existing = await _db.AccommodationRates
                .IgnoreQueryFilters()
                .Where(r => r.HotelId == hotel.Id && r.TenantId == TenantId)
                .ToListAsync(ct);
            _db.AccommodationRates.RemoveRange(existing);

            foreach (var r in request.Rates)
            {
                _db.AccommodationRates.Add(new AccommodationRate
                {
                    TenantId = TenantId,
                    HotelId = hotel.Id,
                    FromDate = NormalizeUtc(r.FromDate),
                    ToDate = NormalizeUtc(r.ToDate),
                    RoomCategory = r.RoomCategory?.Trim(),
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
        return ApiResponse<HotelDto>.Ok(ToHotelDto(updated));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteHotel([FromRoute] Guid id, CancellationToken ct)
    {
        var hotel = await _db.Hotels.FirstOrDefaultAsync(h => h.Id == id && h.TenantId == TenantId, ct);
        if (hotel is null) return NotFound(ApiResponse<object>.Fail("Hotel not found"));
        if (await DenyUnlessModuleActionAsync(_db, ModuleForHotel(hotel.IsHouseboat), ModuleAction.Delete, ct) is { } deleteDenied)
            return deleteDenied;
        hotel.IsDeleted = true;
        hotel.DeletedAtUtc = DateTime.UtcNow;
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
        if (await DenyUnlessModuleActionAsync(_db, ModuleForHotel(hotel.IsHouseboat), ModuleAction.Edit, ct) is { } uploadDenied)
            return uploadDenied;

        hotel.ImageUrls ??= [];
        foreach (var file in files ?? [])
        {
            if (file.Length == 0) continue;
            var url = await _storage.SaveHotelImageAsync(TenantId, id, file, ct);
            hotel.ImageUrls.Add(url);
        }
        await _db.SaveChangesAsync(ct);

        return ApiResponse<List<string>>.Ok(NormalizeImageUrlsForClient(hotel.ImageUrls));
    }

    /// <summary>Remove one image URL from a hotel. Pass stored path (/uploads/...) or absolute URL (browser may send either).</summary>
    [HttpDelete("{id:guid}/images")]
    public async Task<ActionResult<ApiResponse<List<string>>>> RemoveImage([FromRoute] Guid id, [FromQuery] string url, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url)) return BadRequest(ApiResponse<List<string>>.Fail("url is required"));
        var hotel = await _db.Hotels.FirstOrDefaultAsync(h => h.Id == id && h.TenantId == TenantId, ct);
        if (hotel is null) return NotFound(ApiResponse<List<string>>.Fail("Hotel not found"));
        if (await DenyUnlessModuleActionAsync(_db, ModuleForHotel(hotel.IsHouseboat), ModuleAction.Edit, ct) is { } editDenied)
            return editDenied;

        hotel.ImageUrls ??= [];
        var target = NormalizeImagePathForMatch(url.Trim());
        hotel.ImageUrls.RemoveAll(u => string.Equals(NormalizeImagePathForMatch(u), target, StringComparison.OrdinalIgnoreCase));
        await _db.SaveChangesAsync(ct);

        return ApiResponse<List<string>>.Ok(NormalizeImageUrlsForClient(hotel.ImageUrls));
    }

    /// <summary>Match query <c>url</c> to DB entries: relative /uploads/... or full http(s) URL from clients.</summary>
    private static string NormalizeImagePathForMatch(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return "";
        try
        {
            url = Uri.UnescapeDataString(url.Trim());
        }
        catch
        {
            url = url.Trim();
        }

        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var path = new Uri(url).AbsolutePath;
                return path.Length == 0 ? url : path;
            }
            catch
            {
                return url;
            }
        }

        return url.StartsWith('/') ? url : "/" + url;
    }

    [HttpGet("{hotelId:guid}/rooms")]
    public ActionResult<ApiResponse<List<object>>> GetHotelRooms([FromRoute] Guid hotelId)
    {
        return ApiResponse<List<object>>.Ok([]);
    }

    private static DateTime NormalizeUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private HotelDto ToHotelDto(Hotel h) =>
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
            ImageUrls = NormalizeImageUrlsForClient(h.ImageUrls ?? []),
            Rates = (h.Rates ?? []).Select(r => new AccommodationRateDto
            {
                Id = r.Id.ToString("D"),
                HotelId = r.HotelId.ToString("D"),
                FromDate = r.FromDate,
                ToDate = r.ToDate,
                RoomCategory = r.RoomCategory,
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

    private static AppModuleKey ModuleForHotel(bool isHouseboat) =>
        isHouseboat ? AppModuleKey.Houseboats : AppModuleKey.Hotels;
}

