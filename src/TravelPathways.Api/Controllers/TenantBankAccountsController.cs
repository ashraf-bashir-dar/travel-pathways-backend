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
[Route("api/tenant/bank-accounts")]
public sealed class TenantBankAccountsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TenantContext _tenant;

    public TenantBankAccountsController(AppDbContext db, TenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
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

    public sealed class BankAccountDto
    {
        public required string Id { get; init; }
        public required string AccountHolderName { get; init; }
        public required string BankName { get; init; }
        public required string AccountNumber { get; init; }
        public required string IFSC { get; init; }
        public string? Branch { get; init; }
        public int DisplayOrder { get; init; }
    }

    public sealed class CreateBankAccountRequestDto
    {
        public string AccountHolderName { get; set; } = string.Empty;
        public string BankName { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string IFSC { get; set; } = string.Empty;
        public string? Branch { get; set; }
        public int DisplayOrder { get; set; }
    }

    public sealed class UpdateBankAccountRequestDto
    {
        public string AccountHolderName { get; set; } = string.Empty;
        public string BankName { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string IFSC { get; set; } = string.Empty;
        public string? Branch { get; set; }
        public int DisplayOrder { get; set; }
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<BankAccountDto>>>> GetBankAccounts(CancellationToken ct)
    {
        var check = await EnsureBankAndPaymentModuleAsync(ct);
        if (check != null) return check;
        if (!_tenant.TenantId.HasValue)
            return BadRequest(ApiResponse<List<BankAccountDto>>.Fail("Tenant context is missing."));
        var list = await _db.TenantBankAccounts.AsNoTracking()
            .Where(b => b.TenantId == _tenant.TenantId)
            .OrderBy(b => b.DisplayOrder)
            .ThenBy(b => b.CreatedAt)
            .ToListAsync(ct);
        return ApiResponse<List<BankAccountDto>>.Ok(list.Select(ToDto).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<BankAccountDto>>> GetBankAccount([FromRoute] Guid id, CancellationToken ct)
    {
        var check = await EnsureBankAndPaymentModuleAsync(ct);
        if (check != null) return check;
        if (!_tenant.TenantId.HasValue)
            return BadRequest(ApiResponse<BankAccountDto>.Fail("Tenant context is missing."));
        var item = await _db.TenantBankAccounts.AsNoTracking()
            .FirstOrDefaultAsync(b => b.TenantId == _tenant.TenantId && b.Id == id, ct);
        if (item is null) return NotFound(ApiResponse<BankAccountDto>.Fail("Bank account not found."));
        return ApiResponse<BankAccountDto>.Ok(ToDto(item));
    }

    [HttpPost]
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<ActionResult<ApiResponse<BankAccountDto>>> CreateBankAccount([FromBody] CreateBankAccountRequestDto request, CancellationToken ct)
    {
        var check = await EnsureBankAndPaymentModuleAsync(ct);
        if (check != null) return check;
        if (!_tenant.TenantId.HasValue)
            return BadRequest(ApiResponse<BankAccountDto>.Fail("Tenant context is missing."));
        if (string.IsNullOrWhiteSpace(request.AccountHolderName) || string.IsNullOrWhiteSpace(request.BankName)
            || string.IsNullOrWhiteSpace(request.AccountNumber) || string.IsNullOrWhiteSpace(request.IFSC))
            return BadRequest(ApiResponse<BankAccountDto>.Fail("Account holder name, bank name, account number and IFSC are required."));

        var item = new TenantBankAccount
        {
            TenantId = _tenant.TenantId.Value,
            AccountHolderName = request.AccountHolderName.Trim(),
            BankName = request.BankName.Trim(),
            AccountNumber = request.AccountNumber.Trim(),
            IFSC = request.IFSC.Trim(),
            Branch = request.Branch?.Trim(),
            DisplayOrder = request.DisplayOrder
        };
        _db.TenantBankAccounts.Add(item);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetBankAccount), new { id = item.Id }, ApiResponse<BankAccountDto>.Ok(ToDto(item)));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<ActionResult<ApiResponse<BankAccountDto>>> UpdateBankAccount([FromRoute] Guid id, [FromBody] UpdateBankAccountRequestDto request, CancellationToken ct)
    {
        var check = await EnsureBankAndPaymentModuleAsync(ct);
        if (check != null) return check;
        if (!_tenant.TenantId.HasValue)
            return BadRequest(ApiResponse<BankAccountDto>.Fail("Tenant context is missing."));
        var item = await _db.TenantBankAccounts.FirstOrDefaultAsync(b => b.TenantId == _tenant.TenantId && b.Id == id, ct);
        if (item is null) return NotFound(ApiResponse<BankAccountDto>.Fail("Bank account not found."));
        if (string.IsNullOrWhiteSpace(request.AccountHolderName) || string.IsNullOrWhiteSpace(request.BankName)
            || string.IsNullOrWhiteSpace(request.AccountNumber) || string.IsNullOrWhiteSpace(request.IFSC))
            return BadRequest(ApiResponse<BankAccountDto>.Fail("Account holder name, bank name, account number and IFSC are required."));

        item.AccountHolderName = request.AccountHolderName.Trim();
        item.BankName = request.BankName.Trim();
        item.AccountNumber = request.AccountNumber.Trim();
        item.IFSC = request.IFSC.Trim();
        item.Branch = request.Branch?.Trim();
        item.DisplayOrder = request.DisplayOrder;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<BankAccountDto>.Ok(ToDto(item));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteBankAccount([FromRoute] Guid id, CancellationToken ct)
    {
        var check = await EnsureBankAndPaymentModuleAsync(ct);
        if (check != null) return check;
        if (!_tenant.TenantId.HasValue)
            return BadRequest(ApiResponse<object>.Fail("Tenant context is missing."));
        var item = await _db.TenantBankAccounts.FirstOrDefaultAsync(b => b.TenantId == _tenant.TenantId && b.Id == id, ct);
        if (item is null) return NotFound(ApiResponse<object>.Fail("Bank account not found."));
        item.IsDeleted = true;
        item.DeletedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(new { });
    }

    private static BankAccountDto ToDto(TenantBankAccount b) =>
        new()
        {
            Id = b.Id.ToString("D"),
            AccountHolderName = b.AccountHolderName,
            BankName = b.BankName,
            AccountNumber = b.AccountNumber,
            IFSC = b.IFSC,
            Branch = b.Branch,
            DisplayOrder = b.DisplayOrder
        };
}
