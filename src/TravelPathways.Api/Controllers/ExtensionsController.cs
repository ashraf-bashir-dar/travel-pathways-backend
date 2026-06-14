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
[Route("api/extensions")]
public sealed class ExtensionsController : TenantControllerBase
{
    private readonly AppDbContext _db;
    private readonly TenantContext _tenant;

    public ExtensionsController(AppDbContext db, TenantContext tenant) : base(tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public sealed class ExtensionCatalogItemDto
    {
        public required string Id { get; init; }
        public required string Code { get; init; }
        public required string Name { get; init; }
        public required string Summary { get; init; }
        public required string Details { get; init; }
        public required string Icon { get; init; }
        public required string SupportedBrowsers { get; init; }
        public string? ChromeStoreUrl { get; init; }
        public string? EdgeStoreUrl { get; init; }
        public string? DownloadApiPath { get; init; }
        public string? InstallSteps { get; init; }
        public int SortOrder { get; init; }
        public bool IsPublished { get; init; }
    }

    public sealed class UpsertExtensionRequestDto
    {
        public required string Code { get; init; }
        public required string Name { get; init; }
        public required string Summary { get; init; }
        public required string Details { get; init; }
        public string? Icon { get; init; }
        public string? SupportedBrowsers { get; init; }
        public string? ChromeStoreUrl { get; init; }
        public string? EdgeStoreUrl { get; init; }
        public string? DownloadApiPath { get; init; }
        public string? InstallSteps { get; init; }
        public int SortOrder { get; init; }
        public bool IsPublished { get; init; } = true;
    }

    private async Task<ActionResult?> EnsureExtensionsModuleAsync(CancellationToken ct)
    {
        if (_tenant.IsSuperAdmin) return null;
        if (!HasTenantId) return BadRequest(ApiResponse<object>.Fail("Tenant context is missing."));

        var enabled = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == TenantId)
            .Select(t => t.EnabledModules)
            .FirstOrDefaultAsync(ct);

        if (enabled == null || enabled.Count == 0) return null;
        if (enabled.Contains(AppModuleKey.Extensions)) return null;

        return StatusCode(403, ApiResponse<object>.Fail("Extensions module is not enabled for this tenant."));
    }

    private static ExtensionCatalogItemDto ToDto(ExtensionCatalogItem e) => new()
    {
        Id = e.Id.ToString("D"),
        Code = e.Code,
        Name = e.Name,
        Summary = e.Summary,
        Details = e.Details,
        Icon = e.Icon,
        SupportedBrowsers = e.SupportedBrowsers,
        ChromeStoreUrl = e.ChromeStoreUrl,
        EdgeStoreUrl = e.EdgeStoreUrl,
        DownloadApiPath = e.DownloadApiPath,
        InstallSteps = e.InstallSteps,
        SortOrder = e.SortOrder,
        IsPublished = e.IsPublished
    };

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<ExtensionCatalogItemDto>>>> List(
        [FromQuery] bool includeUnpublished = false,
        CancellationToken ct = default)
    {
        var moduleCheck = await EnsureExtensionsModuleAsync(ct);
        if (moduleCheck != null) return moduleCheck;

        if (!HasTenantId)
            return BadRequest(ApiResponse<List<ExtensionCatalogItemDto>>.Fail("Tenant context is missing."));

        await ExtensionCatalogSeeder.EnsureDefaultsForTenantAsync(_db, TenantId, ct);

        var query = _db.ExtensionCatalogItems.AsNoTracking()
            .Where(e => e.TenantId == TenantId);

        if (!includeUnpublished || !IsTenantAdmin())
            query = query.Where(e => e.IsPublished);

        var list = await query
            .OrderBy(e => e.SortOrder)
            .ThenBy(e => e.Name)
            .ToListAsync(ct);

        return ApiResponse<List<ExtensionCatalogItemDto>>.Ok(list.Select(ToDto).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ExtensionCatalogItemDto>>> GetById(
        [FromRoute] Guid id,
        CancellationToken ct = default)
    {
        var moduleCheck = await EnsureExtensionsModuleAsync(ct);
        if (moduleCheck != null) return moduleCheck;

        if (!HasTenantId)
            return BadRequest(ApiResponse<ExtensionCatalogItemDto>.Fail("Tenant context is missing."));

        var item = await _db.ExtensionCatalogItems.AsNoTracking()
            .FirstOrDefaultAsync(e => e.TenantId == TenantId && e.Id == id, ct);

        if (item is null)
            return NotFound(ApiResponse<ExtensionCatalogItemDto>.Fail("Extension not found."));

        if (!item.IsPublished && !IsTenantAdmin())
            return NotFound(ApiResponse<ExtensionCatalogItemDto>.Fail("Extension not found."));

        return ApiResponse<ExtensionCatalogItemDto>.Ok(ToDto(item));
    }

    [HttpPost]
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<ActionResult<ApiResponse<ExtensionCatalogItemDto>>> Create(
        [FromBody] UpsertExtensionRequestDto dto,
        CancellationToken ct = default)
    {
        var moduleCheck = await EnsureExtensionsModuleAsync(ct);
        if (moduleCheck != null) return moduleCheck;

        if (!HasTenantId)
            return BadRequest(ApiResponse<ExtensionCatalogItemDto>.Fail("Tenant context is missing."));

        var code = (dto.Code ?? string.Empty).Trim().ToLowerInvariant();
        if (code.Length == 0)
            return BadRequest(ApiResponse<ExtensionCatalogItemDto>.Fail("Code is required."));

        var exists = await _db.ExtensionCatalogItems.AnyAsync(e => e.TenantId == TenantId && e.Code == code, ct);
        if (exists)
            return BadRequest(ApiResponse<ExtensionCatalogItemDto>.Fail("An extension with this code already exists."));

        var entity = MapToEntity(new ExtensionCatalogItem { TenantId = TenantId }, dto, code);
        _db.ExtensionCatalogItems.Add(entity);
        await _db.SaveChangesAsync(ct);

        return ApiResponse<ExtensionCatalogItemDto>.Ok(ToDto(entity));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<ActionResult<ApiResponse<ExtensionCatalogItemDto>>> Update(
        [FromRoute] Guid id,
        [FromBody] UpsertExtensionRequestDto dto,
        CancellationToken ct = default)
    {
        var moduleCheck = await EnsureExtensionsModuleAsync(ct);
        if (moduleCheck != null) return moduleCheck;

        if (!HasTenantId)
            return BadRequest(ApiResponse<ExtensionCatalogItemDto>.Fail("Tenant context is missing."));

        var entity = await _db.ExtensionCatalogItems
            .FirstOrDefaultAsync(e => e.TenantId == TenantId && e.Id == id, ct);

        if (entity is null)
            return NotFound(ApiResponse<ExtensionCatalogItemDto>.Fail("Extension not found."));

        var code = (dto.Code ?? string.Empty).Trim().ToLowerInvariant();
        if (code.Length == 0)
            return BadRequest(ApiResponse<ExtensionCatalogItemDto>.Fail("Code is required."));

        var duplicate = await _db.ExtensionCatalogItems.AnyAsync(
            e => e.TenantId == TenantId && e.Code == code && e.Id != id, ct);
        if (duplicate)
            return BadRequest(ApiResponse<ExtensionCatalogItemDto>.Fail("An extension with this code already exists."));

        MapToEntity(entity, dto, code);
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return ApiResponse<ExtensionCatalogItemDto>.Ok(ToDto(entity));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<ActionResult<ApiResponse<object>>> Delete([FromRoute] Guid id, CancellationToken ct = default)
    {
        var moduleCheck = await EnsureExtensionsModuleAsync(ct);
        if (moduleCheck != null) return moduleCheck;

        if (!HasTenantId)
            return BadRequest(ApiResponse<object>.Fail("Tenant context is missing."));

        var entity = await _db.ExtensionCatalogItems
            .FirstOrDefaultAsync(e => e.TenantId == TenantId && e.Id == id, ct);

        if (entity is null)
            return NotFound(ApiResponse<object>.Fail("Extension not found."));

        entity.IsDeleted = true;
        entity.DeletedAtUtc = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return ApiResponse<object>.Ok(new { deleted = true });
    }

    private static ExtensionCatalogItem MapToEntity(
        ExtensionCatalogItem entity,
        UpsertExtensionRequestDto dto,
        string code)
    {
        entity.Code = code.Length > 64 ? code[..64] : code;
        entity.Name = TrimMax(dto.Name, 200);
        entity.Summary = TrimMax(dto.Summary, 500);
        entity.Details = dto.Details?.Trim() ?? string.Empty;
        entity.Icon = TrimMax(dto.Icon ?? "🧩", 16);
        entity.SupportedBrowsers = TrimMax(dto.SupportedBrowsers ?? "Chrome, Edge", 120);
        entity.ChromeStoreUrl = TrimOptional(dto.ChromeStoreUrl, 1000);
        entity.EdgeStoreUrl = TrimOptional(dto.EdgeStoreUrl, 1000);
        entity.DownloadApiPath = TrimOptional(dto.DownloadApiPath, 256);
        entity.InstallSteps = string.IsNullOrWhiteSpace(dto.InstallSteps) ? null : dto.InstallSteps.Trim();
        entity.SortOrder = dto.SortOrder;
        entity.IsPublished = dto.IsPublished;
        return entity;
    }

    private static string TrimMax(string value, int max)
    {
        var t = (value ?? string.Empty).Trim();
        return t.Length > max ? t[..max] : t;
    }

    private static string? TrimOptional(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var t = value.Trim();
        return t.Length > max ? t[..max] : t;
    }
}

internal static class ExtensionCatalogSeeder
{
    internal static async Task EnsureDefaultsForTenantAsync(AppDbContext db, Guid tenantId, CancellationToken ct)
    {
        var hasAny = await db.ExtensionCatalogItems.AnyAsync(e => e.TenantId == tenantId, ct);
        if (hasAny) return;

        db.ExtensionCatalogItems.Add(new ExtensionCatalogItem
        {
            TenantId = tenantId,
            Code = "browser-activity",
            Name = "Travel Pathways Activity",
            Summary = "Reports websites visited in Chrome or Edge to your admin (Employee → Activity).",
            Details =
                "Install this browser extension on employee PCs to log websites visited during work " +
                "(Facebook, Google, partner sites, etc.). Employees sign in once with their Travel Pathways account. " +
                "Requires activity tracking to be enabled for the user.",
            Icon = "🌐",
            SupportedBrowsers = "Chrome, Edge",
            DownloadApiPath = "user-activity/extension-download",
            SortOrder = 0,
            IsPublished = true
        });

        await db.SaveChangesAsync(ct);
    }
}
