using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelPathways.Api.Common;
using TravelPathways.Api.Data;
using TravelPathways.Api.Data.Entities;
using TravelPathways.Api.MultiTenancy;

namespace TravelPathways.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/packages")]
public sealed class PackagesController : TenantControllerBase
{
    private readonly AppDbContext _db;

    public PackagesController(AppDbContext db, TenantContext tenant) : base(tenant)
    {
        _db = db;
    }

    public sealed class DayItineraryDto
    {
        public required string Id { get; init; }
        public required string PackageId { get; init; }
        public required int DayNumber { get; init; }
        public required DateTime Date { get; init; }
        public string? HotelId { get; init; }
        public string? HotelName { get; init; }
        public string? RoomType { get; init; }
        public required int NumberOfRooms { get; init; }
        public string? CheckInTime { get; init; }
        public string? CheckOutTime { get; init; }
        public AccommodationMealPlan? MealPlan { get; init; }
        public int? ExtraBedCount { get; init; }
        public int? CnbCount { get; init; }
        public List<string>? Activities { get; init; }
        public List<string>? Meals { get; init; }
        public string? Notes { get; init; }
        public required decimal HotelCost { get; init; }
    }

    public sealed class PackageDto
    {
        public required string Id { get; init; }
        public string? LeadId { get; init; }
        public required string ClientName { get; init; }
        public required string ClientPhone { get; init; }
        public string? ClientEmail { get; init; }
        public string? ClientCity { get; init; }
        public string? ClientState { get; init; }
        public required string ClientPickupLocation { get; init; }
        public required string ClientDropLocation { get; init; }
        public required string PackageName { get; init; }
        public required DateTime StartDate { get; init; }
        public required DateTime EndDate { get; init; }
        public required int NumberOfDays { get; init; }
        public required int NumberOfAdults { get; init; }
        public required int NumberOfChildren { get; init; }
        public string? VehicleId { get; init; }
        public string? VehicleName { get; init; }
        public decimal? VehicleRate { get; init; }
        public required decimal TotalAmount { get; init; }
        public required decimal AdvanceAmount { get; init; }
        public required decimal BalanceAmount { get; init; }
        public required PackageStatus Status { get; init; }
        public List<DayItineraryDto>? DayWiseItinerary { get; init; }
        public required string TenantId { get; init; }
        public required DateTime CreatedAt { get; init; }
        public required DateTime UpdatedAt { get; init; }
        public required string CreatedBy { get; init; }
    }

    public sealed class CreateDayItineraryRequestDto
    {
        public int DayNumber { get; set; }
        public DateTime Date { get; set; }
        public Guid? HotelId { get; set; }
        public string? RoomType { get; set; }
        public int NumberOfRooms { get; set; }
        public string? CheckInTime { get; set; }
        public string? CheckOutTime { get; set; }
        public AccommodationMealPlan? MealPlan { get; set; }
        public int? ExtraBedCount { get; set; }
        public int? CnbCount { get; set; }
        public List<string>? Activities { get; set; }
        public List<string>? Meals { get; set; }
        public string? Notes { get; set; }
        public decimal HotelCost { get; set; }
    }

    public class CreatePackageRequestDto
    {
        public Guid? LeadId { get; set; }
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
        public int NumberOfAdults { get; set; }
        public int NumberOfChildren { get; set; }
        public Guid? VehicleId { get; set; }
        public List<CreateDayItineraryRequestDto> DayWiseItinerary { get; set; } = [];
    }

    public sealed class UpdatePackageRequestDto : CreatePackageRequestDto
    {
        public decimal TotalAmount { get; set; }
        public decimal AdvanceAmount { get; set; }
        public PackageStatus Status { get; set; } = PackageStatus.Draft;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PaginatedResponse<PackageDto>>>> GetPackages(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? searchTerm = null,
        CancellationToken ct = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.Packages.AsNoTracking()
            .Include(p => p.DayWiseItinerary)
            .Include(p => p.Vehicle)
            .Where(p => p.TenantId == TenantId);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var s = searchTerm.Trim().ToLower();
            query = query.Where(p =>
                p.ClientName.ToLower().Contains(s) ||
                p.ClientPhone.ToLower().Contains(s) ||
                p.PackageName.ToLower().Contains(s));
        }

        var total = await query.CountAsync(ct);
        var list = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = list.Select(ToDto).ToList();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        return ApiResponse<PaginatedResponse<PackageDto>>.Ok(new PaginatedResponse<PackageDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalPages = totalPages
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<PackageDto>>> GetPackageById([FromRoute] Guid id, CancellationToken ct)
    {
        var pkg = await _db.Packages.AsNoTracking()
            .Include(p => p.DayWiseItinerary)
            .ThenInclude(d => d.Hotel)
            .Include(p => p.Vehicle)
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == TenantId, ct);

        if (pkg is null) return NotFound(ApiResponse<PackageDto>.Fail("Package not found"));
        return ApiResponse<PackageDto>.Ok(ToDto(pkg));
    }

    [HttpGet("by-lead/{leadId:guid}")]
    public async Task<ActionResult<ApiResponse<List<PackageDto>>>> GetByLead([FromRoute] Guid leadId, CancellationToken ct)
    {
        var pkgs = await _db.Packages.AsNoTracking()
            .Include(p => p.DayWiseItinerary)
            .Include(p => p.Vehicle)
            .Where(p => p.TenantId == TenantId && p.LeadId == leadId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

        return ApiResponse<List<PackageDto>>.Ok(pkgs.Select(ToDto).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<PackageDto>>> CreatePackage([FromBody] CreatePackageRequestDto request, CancellationToken ct)
    {
        var createdBy = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? "system";

        // Validate referenced entities in same tenant
        if (request.VehicleId is not null)
        {
            var vehicleOk = await _db.Vehicles.AnyAsync(v => v.Id == request.VehicleId && v.TenantId == TenantId, ct);
            if (!vehicleOk) return BadRequest(ApiResponse<PackageDto>.Fail("Vehicle not found"));
        }

        if (request.LeadId is not null)
        {
            var leadOk = await _db.Leads.AnyAsync(l => l.Id == request.LeadId && l.TenantId == TenantId, ct);
            if (!leadOk) return BadRequest(ApiResponse<PackageDto>.Fail("Lead not found"));
        }

        var total = request.DayWiseItinerary.Sum(d => d.HotelCost);
        var pkg = new TourPackage
        {
            TenantId = TenantId,
            LeadId = request.LeadId,
            ClientName = request.ClientName.Trim(),
            ClientPhone = request.ClientPhone.Trim(),
            ClientEmail = request.ClientEmail?.Trim(),
            ClientCity = request.ClientCity?.Trim(),
            ClientState = request.ClientState?.Trim(),
            ClientPickupLocation = request.ClientPickupLocation.Trim(),
            ClientDropLocation = request.ClientDropLocation.Trim(),
            PackageName = request.PackageName.Trim(),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            NumberOfDays = Math.Max(1, request.DayWiseItinerary.Count),
            NumberOfAdults = request.NumberOfAdults,
            NumberOfChildren = request.NumberOfChildren,
            VehicleId = request.VehicleId,
            TotalAmount = total,
            AdvanceAmount = 0,
            BalanceAmount = total,
            Status = PackageStatus.Draft,
            CreatedBy = createdBy
        };

        _db.Packages.Add(pkg);
        await _db.SaveChangesAsync(ct);

        foreach (var d in request.DayWiseItinerary)
        {
            _db.DayItineraries.Add(new DayItinerary
            {
                TenantId = TenantId,
                PackageId = pkg.Id,
                DayNumber = d.DayNumber,
                Date = d.Date,
                HotelId = d.HotelId,
                RoomType = d.RoomType?.Trim(),
                NumberOfRooms = d.NumberOfRooms,
                CheckInTime = d.CheckInTime?.Trim(),
                CheckOutTime = d.CheckOutTime?.Trim(),
                MealPlan = d.MealPlan ?? AccommodationMealPlan.MAP,
                ExtraBedCount = d.ExtraBedCount,
                CnbCount = d.CnbCount,
                Activities = d.Activities ?? [],
                Meals = d.Meals ?? [],
                Notes = d.Notes?.Trim(),
                HotelCost = d.HotelCost
            });
        }

        await _db.SaveChangesAsync(ct);

        var created = await _db.Packages.AsNoTracking()
            .Include(p => p.DayWiseItinerary).ThenInclude(d => d.Hotel)
            .Include(p => p.Vehicle)
            .FirstAsync(p => p.Id == pkg.Id, ct);

        return CreatedAtAction(nameof(GetPackageById), new { id = pkg.Id }, ApiResponse<PackageDto>.Ok(ToDto(created)));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<PackageDto>>> UpdatePackage([FromRoute] Guid id, [FromBody] UpdatePackageRequestDto request, CancellationToken ct)
    {
        var pkg = await _db.Packages.Include(p => p.DayWiseItinerary).FirstOrDefaultAsync(p => p.Id == id && p.TenantId == TenantId, ct);
        if (pkg is null) return NotFound(ApiResponse<PackageDto>.Fail("Package not found"));

        if (request.VehicleId is not null)
        {
            var vehicleOk = await _db.Vehicles.AnyAsync(v => v.Id == request.VehicleId && v.TenantId == TenantId, ct);
            if (!vehicleOk) return BadRequest(ApiResponse<PackageDto>.Fail("Vehicle not found"));
        }

        pkg.LeadId = request.LeadId;
        pkg.ClientName = request.ClientName.Trim();
        pkg.ClientPhone = request.ClientPhone.Trim();
        pkg.ClientEmail = request.ClientEmail?.Trim();
        pkg.ClientCity = request.ClientCity?.Trim();
        pkg.ClientState = request.ClientState?.Trim();
        pkg.ClientPickupLocation = request.ClientPickupLocation.Trim();
        pkg.ClientDropLocation = request.ClientDropLocation.Trim();
        pkg.PackageName = request.PackageName.Trim();
        pkg.StartDate = request.StartDate;
        pkg.EndDate = request.EndDate;
        pkg.NumberOfDays = Math.Max(1, request.DayWiseItinerary.Count);
        pkg.NumberOfAdults = request.NumberOfAdults;
        pkg.NumberOfChildren = request.NumberOfChildren;
        pkg.VehicleId = request.VehicleId;
        pkg.TotalAmount = request.TotalAmount;
        pkg.AdvanceAmount = request.AdvanceAmount;
        pkg.BalanceAmount = request.TotalAmount - request.AdvanceAmount;
        pkg.Status = request.Status;

        // Replace itinerary
        var existing = await _db.DayItineraries.Where(d => d.PackageId == pkg.Id && d.TenantId == TenantId).ToListAsync(ct);
        _db.DayItineraries.RemoveRange(existing);

        foreach (var d in request.DayWiseItinerary)
        {
            _db.DayItineraries.Add(new DayItinerary
            {
                TenantId = TenantId,
                PackageId = pkg.Id,
                DayNumber = d.DayNumber,
                Date = d.Date,
                HotelId = d.HotelId,
                RoomType = d.RoomType?.Trim(),
                NumberOfRooms = d.NumberOfRooms,
                CheckInTime = d.CheckInTime?.Trim(),
                CheckOutTime = d.CheckOutTime?.Trim(),
                MealPlan = d.MealPlan ?? AccommodationMealPlan.MAP,
                ExtraBedCount = d.ExtraBedCount,
                CnbCount = d.CnbCount,
                Activities = d.Activities ?? [],
                Meals = d.Meals ?? [],
                Notes = d.Notes?.Trim(),
                HotelCost = d.HotelCost
            });
        }

        await _db.SaveChangesAsync(ct);

        var updated = await _db.Packages.AsNoTracking()
            .Include(p => p.DayWiseItinerary).ThenInclude(d => d.Hotel)
            .Include(p => p.Vehicle)
            .FirstAsync(p => p.Id == pkg.Id, ct);

        return ApiResponse<PackageDto>.Ok(ToDto(updated));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> DeletePackage([FromRoute] Guid id, CancellationToken ct)
    {
        var pkg = await _db.Packages.FirstOrDefaultAsync(p => p.Id == id && p.TenantId == TenantId, ct);
        if (pkg is null) return NotFound(ApiResponse<object>.Fail("Package not found"));
        _db.Packages.Remove(pkg);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(new { });
    }

    private static PackageDto ToDto(TourPackage p)
    {
        var vehicleName = p.Vehicle is null
            ? null
            : $"{p.Vehicle.VehicleType}{(string.IsNullOrWhiteSpace(p.Vehicle.VehicleModel) ? "" : " - " + p.Vehicle.VehicleModel)}";

        return new PackageDto
        {
            Id = p.Id.ToString("D"),
            LeadId = p.LeadId?.ToString("D"),
            ClientName = p.ClientName,
            ClientPhone = p.ClientPhone,
            ClientEmail = p.ClientEmail,
            ClientCity = p.ClientCity,
            ClientState = p.ClientState,
            ClientPickupLocation = p.ClientPickupLocation,
            ClientDropLocation = p.ClientDropLocation,
            PackageName = p.PackageName,
            StartDate = p.StartDate,
            EndDate = p.EndDate,
            NumberOfDays = p.NumberOfDays,
            NumberOfAdults = p.NumberOfAdults,
            NumberOfChildren = p.NumberOfChildren,
            VehicleId = p.VehicleId?.ToString("D"),
            VehicleName = vehicleName,
            VehicleRate = null,
            TotalAmount = p.TotalAmount,
            AdvanceAmount = p.AdvanceAmount,
            BalanceAmount = p.BalanceAmount,
            Status = p.Status,
            DayWiseItinerary = p.DayWiseItinerary
                .OrderBy(d => d.DayNumber)
                .Select(d => new DayItineraryDto
                {
                    Id = d.Id.ToString("D"),
                    PackageId = d.PackageId.ToString("D"),
                    DayNumber = d.DayNumber,
                    Date = d.Date,
                    HotelId = d.HotelId?.ToString("D"),
                    HotelName = d.Hotel?.Name,
                    RoomType = d.RoomType,
                    NumberOfRooms = d.NumberOfRooms,
                    CheckInTime = d.CheckInTime,
                    CheckOutTime = d.CheckOutTime,
                    MealPlan = d.MealPlan,
                    ExtraBedCount = d.ExtraBedCount,
                    CnbCount = d.CnbCount,
                    Activities = d.Activities,
                    Meals = d.Meals,
                    Notes = d.Notes,
                    HotelCost = d.HotelCost
                }).ToList(),
            TenantId = p.TenantId.ToString("D"),
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt,
            CreatedBy = p.CreatedBy
        };
    }
}

