using System.Text.Json.Serialization;
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
[Route("api/vehicle-pricing")]
public sealed class VehiclePricingController : TenantControllerBase
{
    private readonly AppDbContext _db;

    public VehiclePricingController(AppDbContext db, TenantContext tenant) : base(tenant)
    {
        _db = db;
    }

    public sealed class CreateVehiclePricingRequestDto
    {
        [JsonPropertyName("vehicleId")]
        public Guid VehicleId { get; set; }

        [JsonPropertyName("pickupLocation")]
        public string PickupLocation { get; set; } = string.Empty;

        [JsonPropertyName("dropLocation")]
        public string DropLocation { get; set; } = string.Empty;

        [JsonPropertyName("costPrice")]
        public decimal? CostPrice { get; set; }

        [JsonPropertyName("sellingPrice")]
        public decimal? SellingPrice { get; set; }

        [JsonPropertyName("fromDate")]
        public DateTime? FromDate { get; set; }

        [JsonPropertyName("toDate")]
        public DateTime? ToDate { get; set; }

        [JsonPropertyName("rate")]
        public decimal? Rate { get; set; }

        [JsonPropertyName("rateType")]
        public RateType? RateType { get; set; }

        [JsonPropertyName("effectiveFrom")]
        public DateTime? EffectiveFrom { get; set; }

        [JsonPropertyName("effectiveTo")]
        public DateTime? EffectiveTo { get; set; }
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
        public decimal? Rate { get; init; }
        public RateType? RateType { get; init; }
        public DateTime? EffectiveFrom { get; init; }
        public DateTime? EffectiveTo { get; init; }
        public required string TenantId { get; init; }
        public required DateTime CreatedAt { get; init; }
        public required DateTime UpdatedAt { get; init; }
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<VehiclePricingDto>>> Create([FromBody] CreateVehiclePricingRequestDto request, CancellationToken ct)
    {
        var vehicleExists = await _db.Vehicles.AnyAsync(v => v.Id == request.VehicleId && v.TenantId == TenantId, ct);
        if (!vehicleExists) return BadRequest(ApiResponse<VehiclePricingDto>.Fail("Vehicle not found"));

        var selling = request.SellingPrice ?? request.Rate ?? 0m;
        var cost = request.CostPrice ?? 0m;
        var from = request.FromDate ?? request.EffectiveFrom ?? DateTime.UtcNow.Date;
        var to = request.ToDate ?? request.EffectiveTo;

        var pricing = new VehiclePricing
        {
            TenantId = TenantId,
            VehicleId = request.VehicleId,
            PickupLocation = request.PickupLocation.Trim(),
            DropLocation = request.DropLocation.Trim(),
            CostPrice = cost,
            SellingPrice = selling,
            FromDate = from,
            ToDate = to
        };

        _db.VehiclePricing.Add(pricing);
        await _db.SaveChangesAsync(ct);

        return ApiResponse<VehiclePricingDto>.Ok(ToDto(pricing, request.RateType));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<VehiclePricingDto>>> Update([FromRoute] Guid id, [FromBody] CreateVehiclePricingRequestDto request, CancellationToken ct)
    {
        var pricing = await _db.VehiclePricing.FirstOrDefaultAsync(p => p.Id == id && p.TenantId == TenantId, ct);
        if (pricing is null) return NotFound(ApiResponse<VehiclePricingDto>.Fail("Pricing not found"));

        // VehicleId may change (same tenant) - validate
        var vehicleExists = await _db.Vehicles.AnyAsync(v => v.Id == request.VehicleId && v.TenantId == TenantId, ct);
        if (!vehicleExists) return BadRequest(ApiResponse<VehiclePricingDto>.Fail("Vehicle not found"));

        var selling = request.SellingPrice ?? request.Rate ?? pricing.SellingPrice;
        var cost = request.CostPrice ?? pricing.CostPrice;
        var from = request.FromDate ?? request.EffectiveFrom ?? pricing.FromDate;
        var to = request.ToDate ?? request.EffectiveTo;

        pricing.VehicleId = request.VehicleId;
        pricing.PickupLocation = request.PickupLocation.Trim();
        pricing.DropLocation = request.DropLocation.Trim();
        pricing.CostPrice = cost;
        pricing.SellingPrice = selling;
        pricing.FromDate = from;
        pricing.ToDate = to;

        await _db.SaveChangesAsync(ct);
        return ApiResponse<VehiclePricingDto>.Ok(ToDto(pricing, request.RateType));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete([FromRoute] Guid id, CancellationToken ct)
    {
        var pricing = await _db.VehiclePricing.FirstOrDefaultAsync(p => p.Id == id && p.TenantId == TenantId, ct);
        if (pricing is null) return NotFound(ApiResponse<object>.Fail("Pricing not found"));
        _db.VehiclePricing.Remove(pricing);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(new { });
    }

    private static VehiclePricingDto ToDto(VehiclePricing p, RateType? rateType) =>
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
            RateType = rateType,
            EffectiveFrom = p.FromDate,
            EffectiveTo = p.ToDate,
            TenantId = p.TenantId.ToString("D"),
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt
        };
}

