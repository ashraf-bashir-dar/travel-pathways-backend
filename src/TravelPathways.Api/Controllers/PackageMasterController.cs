using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelPathways.Api.Common;
using TravelPathways.Api.Data;
using TravelPathways.Api.Data.Entities;
using TravelPathways.Api.MultiTenancy;
using TravelPathways.Api.Services;

namespace TravelPathways.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/package-master")]
public sealed class PackageMasterController : TenantControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPackageMasterDataService _masterData;

    public PackageMasterController(AppDbContext db, TenantContext tenant, IPackageMasterDataService masterData) : base(tenant)
    {
        _db = db;
        _masterData = masterData;
    }

    public sealed class InclusionMasterDto
    {
        public required string Id { get; init; }
        public required string Code { get; init; }
        public required string Label { get; init; }
        public required int SortOrder { get; init; }
        public required bool IsInclusion { get; init; }
        public required bool IsActive { get; init; }
    }

    public sealed class LocationMasterDto
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
        public required bool AllowPickup { get; init; }
        public required bool AllowDrop { get; init; }
        public required int SortOrder { get; init; }
        public required bool IsActive { get; init; }
    }

    public sealed class CreateInclusionRequestDto
    {
        public string? Code { get; set; }
        public string Label { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public bool IsInclusion { get; set; } = true;
    }

    public sealed class UpdateInclusionRequestDto
    {
        public string? Label { get; set; }
        public int? SortOrder { get; set; }
        public bool? IsInclusion { get; set; }
        public bool? IsActive { get; set; }
    }

    public sealed class CreateLocationRequestDto
    {
        public string Name { get; set; } = string.Empty;
        public bool AllowPickup { get; set; } = true;
        public bool AllowDrop { get; set; } = true;
        public int SortOrder { get; set; }
    }

    public sealed class UpdateLocationRequestDto
    {
        public string? Name { get; set; }
        public bool? AllowPickup { get; set; }
        public bool? AllowDrop { get; set; }
        public int? SortOrder { get; set; }
        public bool? IsActive { get; set; }
    }

    [HttpGet("inclusions")]
    public async Task<ActionResult<ApiResponse<List<InclusionMasterDto>>>> GetInclusions(CancellationToken ct)
    {
        var rows = await _masterData.GetInclusionsAsync(TenantId, ct);
        return ApiResponse<List<InclusionMasterDto>>.Ok(rows.Select(ToInclusionDto).ToList());
    }

    [HttpPost("inclusions")]
    public async Task<ActionResult<ApiResponse<InclusionMasterDto>>> CreateInclusion(
        [FromBody] CreateInclusionRequestDto request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Label))
            return BadRequest(ApiResponse<InclusionMasterDto>.Fail("Label is required."));

        var code = NormalizeCode(request.Code, request.Label);
        var exists = await _db.PackageInclusionMasters
            .AnyAsync(x => x.TenantId == TenantId && x.Code == code && !x.IsDeleted, ct);
        if (exists)
            return BadRequest(ApiResponse<InclusionMasterDto>.Fail("An item with this code already exists."));

        var entity = new PackageInclusionMaster
        {
            TenantId = TenantId,
            Code = code,
            Label = request.Label.Trim(),
            SortOrder = request.SortOrder,
            IsInclusion = request.IsInclusion
        };
        _db.PackageInclusionMasters.Add(entity);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<InclusionMasterDto>.Ok(ToInclusionDto(entity));
    }

    [HttpPut("inclusions/{id:guid}")]
    public async Task<ActionResult<ApiResponse<InclusionMasterDto>>> UpdateInclusion(
        [FromRoute] Guid id,
        [FromBody] UpdateInclusionRequestDto request,
        CancellationToken ct)
    {
        var entity = await _db.PackageInclusionMasters
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == TenantId && !x.IsDeleted, ct);
        if (entity is null)
            return NotFound(ApiResponse<InclusionMasterDto>.Fail("Inclusion not found."));

        if (request.Label is { } label) entity.Label = label.Trim();
        if (request.SortOrder is { } sort) entity.SortOrder = sort;
        if (request.IsInclusion is { } isInclusion) entity.IsInclusion = isInclusion;
        if (request.IsActive is { } active) entity.IsActive = active;

        await _db.SaveChangesAsync(ct);
        return ApiResponse<InclusionMasterDto>.Ok(ToInclusionDto(entity));
    }

    [HttpDelete("inclusions/{id:guid}")]
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteInclusion([FromRoute] Guid id, CancellationToken ct)
    {
        var entity = await _db.PackageInclusionMasters
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == TenantId && !x.IsDeleted, ct);
        if (entity is null)
            return NotFound(ApiResponse<object>.Fail("Inclusion not found."));

        entity.IsDeleted = true;
        entity.DeletedAtUtc = DateTime.UtcNow;
        entity.IsActive = false;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(new { });
    }

    [HttpGet("locations")]
    public async Task<ActionResult<ApiResponse<List<LocationMasterDto>>>> GetLocations(CancellationToken ct)
    {
        var rows = await _masterData.GetLocationsAsync(TenantId, ct);
        return ApiResponse<List<LocationMasterDto>>.Ok(rows.Select(ToLocationDto).ToList());
    }

    [HttpPost("locations")]
    public async Task<ActionResult<ApiResponse<LocationMasterDto>>> CreateLocation(
        [FromBody] CreateLocationRequestDto request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(ApiResponse<LocationMasterDto>.Fail("Name is required."));
        if (!request.AllowPickup && !request.AllowDrop)
            return BadRequest(ApiResponse<LocationMasterDto>.Fail("Enable pickup and/or drop for this location."));

        var name = request.Name.Trim();
        var exists = await _db.PackageLocationMasters
            .AnyAsync(x => x.TenantId == TenantId && x.Name == name && !x.IsDeleted, ct);
        if (exists)
            return BadRequest(ApiResponse<LocationMasterDto>.Fail("A location with this name already exists."));

        var entity = new PackageLocationMaster
        {
            TenantId = TenantId,
            Name = name,
            AllowPickup = request.AllowPickup,
            AllowDrop = request.AllowDrop,
            SortOrder = request.SortOrder
        };
        _db.PackageLocationMasters.Add(entity);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<LocationMasterDto>.Ok(ToLocationDto(entity));
    }

    [HttpPut("locations/{id:guid}")]
    public async Task<ActionResult<ApiResponse<LocationMasterDto>>> UpdateLocation(
        [FromRoute] Guid id,
        [FromBody] UpdateLocationRequestDto request,
        CancellationToken ct)
    {
        var entity = await _db.PackageLocationMasters
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == TenantId && !x.IsDeleted, ct);
        if (entity is null)
            return NotFound(ApiResponse<LocationMasterDto>.Fail("Location not found."));

        if (request.Name is { } name)
        {
            var trimmed = name.Trim();
            var duplicate = await _db.PackageLocationMasters
                .AnyAsync(x => x.TenantId == TenantId && x.Id != id && x.Name == trimmed && !x.IsDeleted, ct);
            if (duplicate)
                return BadRequest(ApiResponse<LocationMasterDto>.Fail("A location with this name already exists."));
            entity.Name = trimmed;
        }

        if (request.AllowPickup is { } pickup) entity.AllowPickup = pickup;
        if (request.AllowDrop is { } drop) entity.AllowDrop = drop;
        if (request.SortOrder is { } sort) entity.SortOrder = sort;
        if (request.IsActive is { } active) entity.IsActive = active;

        if (!entity.AllowPickup && !entity.AllowDrop)
            return BadRequest(ApiResponse<LocationMasterDto>.Fail("Enable pickup and/or drop for this location."));

        await _db.SaveChangesAsync(ct);
        return ApiResponse<LocationMasterDto>.Ok(ToLocationDto(entity));
    }

    [HttpDelete("locations/{id:guid}")]
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteLocation([FromRoute] Guid id, CancellationToken ct)
    {
        var entity = await _db.PackageLocationMasters
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == TenantId && !x.IsDeleted, ct);
        if (entity is null)
            return NotFound(ApiResponse<object>.Fail("Location not found."));

        entity.IsDeleted = true;
        entity.DeletedAtUtc = DateTime.UtcNow;
        entity.IsActive = false;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(new { });
    }

    private static InclusionMasterDto ToInclusionDto(PackageInclusionMaster x) => new()
    {
        Id = x.Id.ToString("D"),
        Code = x.Code,
        Label = x.Label,
        SortOrder = x.SortOrder,
        IsInclusion = x.IsInclusion,
        IsActive = x.IsActive
    };

    private static LocationMasterDto ToLocationDto(PackageLocationMaster x) => new()
    {
        Id = x.Id.ToString("D"),
        Name = x.Name,
        AllowPickup = x.AllowPickup,
        AllowDrop = x.AllowDrop,
        SortOrder = x.SortOrder,
        IsActive = x.IsActive
    };

    private static string NormalizeCode(string? code, string label)
    {
        if (!string.IsNullOrWhiteSpace(code))
        {
            var c = Slugify(code);
            if (c.Length > 0) return c;
        }

        return Slugify(label);
    }

    private static string Slugify(string value)
    {
        var s = value.Trim().ToLowerInvariant();
        s = Regex.Replace(s, @"[^a-z0-9]+", "_");
        s = Regex.Replace(s, @"_+", "_").Trim('_');
        if (s.Length > 64) s = s[..64].TrimEnd('_');
        return s;
    }
}
