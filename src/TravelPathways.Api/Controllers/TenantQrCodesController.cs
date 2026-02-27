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
[Route("api/tenant/qr-codes")]
public sealed class TenantQrCodesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TenantContext _tenant;
    private readonly FileStorage _storage;

    public TenantQrCodesController(AppDbContext db, TenantContext tenant, FileStorage storage)
    {
        _db = db;
        _tenant = tenant;
        _storage = storage;
    }

    /// <summary>Ensure tenant has BankAndPayment module enabled. Null/empty EnabledModules = allow.</summary>
    private async Task<ActionResult?> EnsureBankAndPaymentModuleAsync(CancellationToken ct)
    {
        if (_tenant.IsSuperAdmin) return null;
        if (!_tenant.TenantId.HasValue) return BadRequest(ApiResponse<object>.Fail("Tenant context is missing."));
        var enabled = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == _tenant.TenantId.Value)
            .Select(t => t.EnabledModules)
            .FirstOrDefaultAsync(ct);
        if (enabled == null || enabled.Count == 0) return null;
        if (!enabled.Contains(AppModuleKey.BankAndPayment))
            return StatusCode(403, ApiResponse<object>.Fail("Bank & Payment module is not enabled for this tenant."));
        return null;
    }

    public sealed class QrCodeDto
    {
        public required string Id { get; init; }
        public required string Label { get; init; }
        public required string ImageUrl { get; init; }
        public required string FileName { get; init; }
        public int DisplayOrder { get; init; }
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<QrCodeDto>>>> GetQrCodes(CancellationToken ct)
    {
        var check = await EnsureBankAndPaymentModuleAsync(ct);
        if (check != null) return check;
        if (!_tenant.TenantId.HasValue)
            return BadRequest(ApiResponse<List<QrCodeDto>>.Fail("Tenant context is missing."));
        var list = await _db.TenantQrCodes.AsNoTracking()
            .Where(q => q.TenantId == _tenant.TenantId)
            .OrderBy(q => q.DisplayOrder)
            .ThenBy(q => q.CreatedAt)
            .ToListAsync(ct);
        return ApiResponse<List<QrCodeDto>>.Ok(list.Select(ToDto).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<QrCodeDto>>> GetQrCode([FromRoute] Guid id, CancellationToken ct)
    {
        var check = await EnsureBankAndPaymentModuleAsync(ct);
        if (check != null) return check;
        if (!_tenant.TenantId.HasValue)
            return BadRequest(ApiResponse<QrCodeDto>.Fail("Tenant context is missing."));
        var item = await _db.TenantQrCodes.AsNoTracking()
            .FirstOrDefaultAsync(q => q.TenantId == _tenant.TenantId && q.Id == id, ct);
        if (item is null) return NotFound(ApiResponse<QrCodeDto>.Fail("QR code not found."));
        return ApiResponse<QrCodeDto>.Ok(ToDto(item));
    }

    [HttpPost]
    [Authorize(Policy = "TenantAdminOnly")]
    [RequestSizeLimit(5_242_880)] // 5 MB
    public async Task<ActionResult<ApiResponse<QrCodeDto>>> CreateQrCode([FromForm] string label, [FromForm] int displayOrder, IFormFile? file, CancellationToken ct)
    {
        var check = await EnsureBankAndPaymentModuleAsync(ct);
        if (check != null) return check;
        if (!_tenant.TenantId.HasValue)
            return BadRequest(ApiResponse<QrCodeDto>.Fail("Tenant context is missing."));
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<QrCodeDto>.Fail("Image file is required."));
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".jpg" && ext != ".jpeg" && ext != ".png" && ext != ".gif" && ext != ".webp")
            return BadRequest(ApiResponse<QrCodeDto>.Fail("Only image files (jpg, png, gif, webp) are allowed."));

        var url = await _storage.SaveTenantFileAsync(_tenant.TenantId.Value, "qrcodes", file, ct);
        var item = new TenantQrCode
        {
            TenantId = _tenant.TenantId.Value,
            Label = (label ?? "").Trim().Length > 0 ? label!.Trim() : "QR",
            ImageUrl = url,
            FileName = file.FileName,
            DisplayOrder = displayOrder
        };
        _db.TenantQrCodes.Add(item);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetQrCode), new { id = item.Id }, ApiResponse<QrCodeDto>.Ok(ToDto(item)));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<ActionResult<ApiResponse<QrCodeDto>>> UpdateQrCode([FromRoute] Guid id, [FromBody] UpdateQrCodeRequestDto request, CancellationToken ct)
    {
        var check = await EnsureBankAndPaymentModuleAsync(ct);
        if (check != null) return check;
        if (!_tenant.TenantId.HasValue)
            return BadRequest(ApiResponse<QrCodeDto>.Fail("Tenant context is missing."));
        var item = await _db.TenantQrCodes.FirstOrDefaultAsync(q => q.TenantId == _tenant.TenantId && q.Id == id, ct);
        if (item is null) return NotFound(ApiResponse<QrCodeDto>.Fail("QR code not found."));
        item.Label = (request.Label ?? "").Trim().Length > 0 ? request.Label!.Trim() : "QR";
        item.DisplayOrder = request.DisplayOrder;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<QrCodeDto>.Ok(ToDto(item));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteQrCode([FromRoute] Guid id, CancellationToken ct)
    {
        var check = await EnsureBankAndPaymentModuleAsync(ct);
        if (check != null) return check;
        if (!_tenant.TenantId.HasValue)
            return BadRequest(ApiResponse<object>.Fail("Tenant context is missing."));
        var item = await _db.TenantQrCodes.FirstOrDefaultAsync(q => q.TenantId == _tenant.TenantId && q.Id == id, ct);
        if (item is null) return NotFound(ApiResponse<object>.Fail("QR code not found."));
        item.IsDeleted = true;
        item.DeletedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(new { });
    }

    public sealed class UpdateQrCodeRequestDto
    {
        public string Label { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
    }

    private static QrCodeDto ToDto(TenantQrCode q) =>
        new()
        {
            Id = q.Id.ToString("D"),
            Label = q.Label,
            ImageUrl = q.ImageUrl,
            FileName = q.FileName,
            DisplayOrder = q.DisplayOrder
        };
}
