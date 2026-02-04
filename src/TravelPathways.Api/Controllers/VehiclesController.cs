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
[Route("api/vehicles")]
public sealed class VehiclesController : TenantControllerBase
{
    private readonly AppDbContext _db;

    public VehiclesController(AppDbContext db, TenantContext tenant) : base(tenant)
    {
        _db = db;
    }

    public sealed class VehicleDto
    {
        public required string Id { get; init; }
        public required string TransportCompanyId { get; init; }
        public string? TransportCompanyName { get; init; }
        public required VehicleType VehicleType { get; init; }
        public string? VehicleModel { get; init; }
        public string? VehicleNumber { get; init; }
        public required int SeatingCapacity { get; init; }
        public List<string>? Features { get; init; }
        public required bool IsAcAvailable { get; init; }
        public required string TenantId { get; init; }
        public required bool IsActive { get; init; }
        public required DateTime CreatedAt { get; init; }
        public required DateTime UpdatedAt { get; init; }
    }

    public sealed class CreateVehicleRequestDto
    {
        public Guid TransportCompanyId { get; set; }
        public VehicleType VehicleType { get; set; } = VehicleType.Other;
        public string? VehicleModel { get; set; }
        public string? VehicleNumber { get; set; }
        public int SeatingCapacity { get; set; }
        public List<string>? Features { get; set; }
        public bool IsAcAvailable { get; set; }
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PaginatedResponse<VehicleDto>>>> GetVehicles(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? searchTerm = null,
        [FromQuery] Guid? companyId = null,
        CancellationToken ct = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.Vehicles.AsNoTracking()
            .Include(v => v.TransportCompany)
            .Where(v => v.TenantId == TenantId);

        if (companyId is not null)
        {
            query = query.Where(v => v.TransportCompanyId == companyId.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var s = searchTerm.Trim().ToLower();
            query = query.Where(v =>
                v.VehicleType.ToString().ToLower().Contains(s) ||
                (v.VehicleModel ?? "").ToLower().Contains(s) ||
                (v.VehicleNumber ?? "").ToLower().Contains(s));
        }

        var total = await query.CountAsync(ct);
        var vehicles = await query
            .OrderByDescending(v => v.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        return ApiResponse<PaginatedResponse<VehicleDto>>.Ok(new PaginatedResponse<VehicleDto>
        {
            Items = vehicles.Select(ToDto).ToList(),
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalPages = totalPages
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<VehicleDto>>> GetVehicleById([FromRoute] Guid id, CancellationToken ct)
    {
        var vehicle = await _db.Vehicles.AsNoTracking()
            .Include(v => v.TransportCompany)
            .FirstOrDefaultAsync(v => v.Id == id && v.TenantId == TenantId, ct);
        if (vehicle is null) return NotFound(ApiResponse<VehicleDto>.Fail("Vehicle not found"));
        return ApiResponse<VehicleDto>.Ok(ToDto(vehicle));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<VehicleDto>>> CreateVehicle([FromBody] CreateVehicleRequestDto request, CancellationToken ct)
    {
        var company = await _db.TransportCompanies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.TransportCompanyId && c.TenantId == TenantId, ct);
        if (company is null) return BadRequest(ApiResponse<VehicleDto>.Fail("Transport company not found"));

        var vehicle = new Vehicle
        {
            TenantId = TenantId,
            TransportCompanyId = request.TransportCompanyId,
            VehicleType = request.VehicleType,
            VehicleModel = request.VehicleModel?.Trim(),
            VehicleNumber = request.VehicleNumber?.Trim(),
            SeatingCapacity = request.SeatingCapacity,
            Features = request.Features ?? [],
            IsAcAvailable = request.IsAcAvailable
        };

        _db.Vehicles.Add(vehicle);
        await _db.SaveChangesAsync(ct);

        var created = await _db.Vehicles.AsNoTracking().Include(v => v.TransportCompany).FirstAsync(v => v.Id == vehicle.Id, ct);
        return CreatedAtAction(nameof(GetVehicleById), new { id = vehicle.Id }, ApiResponse<VehicleDto>.Ok(ToDto(created)));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<VehicleDto>>> UpdateVehicle([FromRoute] Guid id, [FromBody] CreateVehicleRequestDto request, CancellationToken ct)
    {
        var vehicle = await _db.Vehicles.FirstOrDefaultAsync(v => v.Id == id && v.TenantId == TenantId, ct);
        if (vehicle is null) return NotFound(ApiResponse<VehicleDto>.Fail("Vehicle not found"));

        // Allow moving vehicle to another company in same tenant
        var companyExists = await _db.TransportCompanies.AnyAsync(c => c.Id == request.TransportCompanyId && c.TenantId == TenantId, ct);
        if (!companyExists) return BadRequest(ApiResponse<VehicleDto>.Fail("Transport company not found"));

        vehicle.TransportCompanyId = request.TransportCompanyId;
        vehicle.VehicleType = request.VehicleType;
        vehicle.VehicleModel = request.VehicleModel?.Trim();
        vehicle.VehicleNumber = request.VehicleNumber?.Trim();
        vehicle.SeatingCapacity = request.SeatingCapacity;
        vehicle.Features = request.Features ?? [];
        vehicle.IsAcAvailable = request.IsAcAvailable;

        await _db.SaveChangesAsync(ct);

        var updated = await _db.Vehicles.AsNoTracking().Include(v => v.TransportCompany).FirstAsync(v => v.Id == vehicle.Id, ct);
        return ApiResponse<VehicleDto>.Ok(ToDto(updated));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteVehicle([FromRoute] Guid id, CancellationToken ct)
    {
        var vehicle = await _db.Vehicles.FirstOrDefaultAsync(v => v.Id == id && v.TenantId == TenantId, ct);
        if (vehicle is null) return NotFound(ApiResponse<object>.Fail("Vehicle not found"));
        _db.Vehicles.Remove(vehicle);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(new { });
    }

    [HttpGet("{vehicleId:guid}/pricing")]
    public async Task<ActionResult<ApiResponse<List<VehiclePricingDto>>>> GetVehiclePricing(
        [FromRoute] Guid vehicleId,
        [FromQuery] string? pickupLocation = null,
        [FromQuery] string? dropLocation = null,
        CancellationToken ct = default)
    {
        var query = _db.VehiclePricing.AsNoTracking()
            .Where(p => p.VehicleId == vehicleId && p.TenantId == TenantId);

        if (!string.IsNullOrWhiteSpace(pickupLocation))
        {
            var s = pickupLocation.Trim().ToLower();
            query = query.Where(p => p.PickupLocation.ToLower() == s);
        }
        if (!string.IsNullOrWhiteSpace(dropLocation))
        {
            var s = dropLocation.Trim().ToLower();
            query = query.Where(p => p.DropLocation.ToLower() == s);
        }

        var list = await query.OrderByDescending(p => p.FromDate).ToListAsync(ct);
        return ApiResponse<List<VehiclePricingDto>>.Ok(list.Select(ToPricingDto).ToList());
    }

    public sealed class VehiclePricingDto
    {
        public required string Id { get; init; }
        public required string VehicleId { get; init; }
        public required string PickupLocation { get; init; }
        public required string DropLocation { get; init; }
        public required decimal CostPrice { get; init; }
        public required decimal SellingPrice { get; init; }
        public required DateTime FromDate { get; init; }
        public DateTime? ToDate { get; init; }

        // Legacy optional fields (frontend sends them sometimes)
        public decimal? Rate { get; init; }
        public RateType? RateType { get; init; }
        public DateTime? EffectiveFrom { get; init; }
        public DateTime? EffectiveTo { get; init; }

        public required string TenantId { get; init; }
        public required DateTime CreatedAt { get; init; }
        public required DateTime UpdatedAt { get; init; }
    }

    private static VehicleDto ToDto(Vehicle v) =>
        new()
        {
            Id = v.Id.ToString("D"),
            TransportCompanyId = v.TransportCompanyId.ToString("D"),
            TransportCompanyName = v.TransportCompany?.Name,
            VehicleType = v.VehicleType,
            VehicleModel = v.VehicleModel,
            VehicleNumber = v.VehicleNumber,
            SeatingCapacity = v.SeatingCapacity,
            Features = v.Features,
            IsAcAvailable = v.IsAcAvailable,
            TenantId = v.TenantId.ToString("D"),
            IsActive = v.IsActive,
            CreatedAt = v.CreatedAt,
            UpdatedAt = v.UpdatedAt
        };

    private static VehiclePricingDto ToPricingDto(VehiclePricing p) =>
        new()
        {
            Id = p.Id.ToString("D"),
            VehicleId = p.VehicleId.ToString("D"),
            PickupLocation = p.PickupLocation,
            DropLocation = p.DropLocation,
            CostPrice = p.CostPrice,
            SellingPrice = p.SellingPrice,
            FromDate = p.FromDate,
            ToDate = p.ToDate,
            Rate = p.SellingPrice,
            RateType = null,
            EffectiveFrom = p.FromDate,
            EffectiveTo = p.ToDate,
            TenantId = p.TenantId.ToString("D"),
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt
        };
}

