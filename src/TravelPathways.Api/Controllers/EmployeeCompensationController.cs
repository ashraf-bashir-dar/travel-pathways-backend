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
[Route("api/tenant/compensation")]
public sealed class EmployeeCompensationController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TenantContext _tenant;

    public EmployeeCompensationController(AppDbContext db, TenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    /// <summary>Ensure tenant has Employee Management module enabled. Null/empty EnabledModules = allow.</summary>
    private async Task<ActionResult?> EnsureEmployeeManagementModuleAsync(CancellationToken ct)
    {
        if (_tenant.IsSuperAdmin) return null;
        if (!_tenant.TenantId.HasValue) return BadRequest(ApiResponse<object>.Fail("Tenant context is missing."));
        var enabled = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == _tenant.TenantId.Value)
            .Select(t => t.EnabledModules)
            .FirstOrDefaultAsync(ct);
        if (enabled == null || enabled.Count == 0) return null;
        if (enabled.Contains(AppModuleKey.EmployeeManagement) || enabled.Contains(AppModuleKey.EmployeeMonitoring))
            return null;
        return StatusCode(403, ApiResponse<object>.Fail("Employee Management module is not enabled for this tenant."));
    }

    public sealed class CompensationDto
    {
        public required string Id { get; init; }
        public required string UserId { get; init; }
        public string? UserName { get; init; }
        public required CompensationType Type { get; init; }
        public required decimal Amount { get; init; }
        public string? PeriodLabel { get; init; }
        public DateTime? PaidOn { get; init; }
        public string? Notes { get; init; }
        public required DateTime CreatedAt { get; init; }
    }

    public sealed class CompensationSummaryDto
    {
        public required string UserId { get; init; }
        public required string UserName { get; init; }
        public decimal TotalSalary { get; init; }
        public decimal TotalIncentive { get; init; }
        public decimal TotalBonus { get; init; }
        public decimal GrandTotal { get; init; }
    }

    public sealed class CreateCompensationRequestDto
    {
        public Guid UserId { get; set; }
        public CompensationType Type { get; set; }
        public decimal Amount { get; set; }
        public string? PeriodLabel { get; set; }
        public DateTime? PaidOn { get; set; }
        public string? Notes { get; set; }
    }

    public sealed class UpdateCompensationRequestDto
    {
        public CompensationType Type { get; set; }
        public decimal Amount { get; set; }
        public string? PeriodLabel { get; set; }
        public DateTime? PaidOn { get; set; }
        public string? Notes { get; set; }
    }

    [HttpGet]
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<ActionResult<ApiResponse<List<CompensationDto>>>> GetList(
        [FromQuery] Guid? userId = null,
        CancellationToken ct = default)
    {
        var moduleCheck = await EnsureEmployeeManagementModuleAsync(ct);
        if (moduleCheck != null) return moduleCheck;
        if (!_tenant.TenantId.HasValue)
            return BadRequest(ApiResponse<List<CompensationDto>>.Fail("Tenant context is missing."));

        var query = _db.EmployeeCompensations.AsNoTracking()
            .Where(c => c.TenantId == _tenant.TenantId);
        if (userId.HasValue)
            query = query.Where(c => c.UserId == userId.Value);
        var list = await query
            .Include(c => c.User)
            .OrderByDescending(c => c.PaidOn ?? c.CreatedAt)
            .ToListAsync(ct);
        var items = list.Select(c => new CompensationDto
        {
            Id = c.Id.ToString("D"),
            UserId = c.UserId.ToString("D"),
            UserName = c.User == null ? null : $"{c.User.FirstName} {c.User.LastName}".Trim(),
            Type = c.Type,
            Amount = c.Amount,
            PeriodLabel = c.PeriodLabel,
            PaidOn = c.PaidOn,
            Notes = c.Notes,
            CreatedAt = c.CreatedAt
        }).ToList();
        return ApiResponse<List<CompensationDto>>.Ok(items);
    }

    [HttpGet("summary")]
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<ActionResult<ApiResponse<List<CompensationSummaryDto>>>> GetSummary(CancellationToken ct = default)
    {
        var moduleCheck = await EnsureEmployeeManagementModuleAsync(ct);
        if (moduleCheck != null) return moduleCheck;
        if (!_tenant.TenantId.HasValue)
            return BadRequest(ApiResponse<List<CompensationSummaryDto>>.Fail("Tenant context is missing."));

        var grouped = await _db.EmployeeCompensations.AsNoTracking()
            .Where(c => c.TenantId == _tenant.TenantId)
            .GroupBy(c => c.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                TotalSalary = g.Where(x => x.Type == CompensationType.Salary).Sum(x => x.Amount),
                TotalIncentive = g.Where(x => x.Type == CompensationType.Incentive).Sum(x => x.Amount),
                TotalBonus = g.Where(x => x.Type == CompensationType.Bonus).Sum(x => x.Amount)
            })
            .ToListAsync(ct);
        var userIds = grouped.Select(x => x.UserId).Distinct().ToList();
        var users = await _db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName })
            .ToListAsync(ct);
        var nameByUserId = users.ToDictionary(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim());
        var items = grouped.Select(g => new CompensationSummaryDto
        {
            UserId = g.UserId.ToString("D"),
            UserName = nameByUserId.TryGetValue(g.UserId, out var n) ? n : "Unknown",
            TotalSalary = g.TotalSalary,
            TotalIncentive = g.TotalIncentive,
            TotalBonus = g.TotalBonus,
            GrandTotal = g.TotalSalary + g.TotalIncentive + g.TotalBonus
        }).OrderByDescending(x => x.GrandTotal).ToList();
        return ApiResponse<List<CompensationSummaryDto>>.Ok(items);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<ActionResult<ApiResponse<CompensationDto>>> GetById([FromRoute] Guid id, CancellationToken ct = default)
    {
        var moduleCheck = await EnsureEmployeeManagementModuleAsync(ct);
        if (moduleCheck != null) return moduleCheck;
        if (!_tenant.TenantId.HasValue)
            return BadRequest(ApiResponse<CompensationDto>.Fail("Tenant context is missing."));
        var c = await _db.EmployeeCompensations.AsNoTracking()
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _tenant.TenantId, ct);
        if (c is null) return NotFound(ApiResponse<CompensationDto>.Fail("Record not found."));
        return ApiResponse<CompensationDto>.Ok(new CompensationDto
        {
            Id = c.Id.ToString("D"),
            UserId = c.UserId.ToString("D"),
            UserName = c.User == null ? null : $"{c.User.FirstName} {c.User.LastName}".Trim(),
            Type = c.Type,
            Amount = c.Amount,
            PeriodLabel = c.PeriodLabel,
            PaidOn = c.PaidOn,
            Notes = c.Notes,
            CreatedAt = c.CreatedAt
        });
    }

    [HttpPost]
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<ActionResult<ApiResponse<CompensationDto>>> Create([FromBody] CreateCompensationRequestDto request, CancellationToken ct = default)
    {
        var moduleCheck = await EnsureEmployeeManagementModuleAsync(ct);
        if (moduleCheck != null) return moduleCheck;
        if (!_tenant.TenantId.HasValue)
            return BadRequest(ApiResponse<CompensationDto>.Fail("Tenant context is missing."));
        var userExists = await _db.Users.AnyAsync(u => u.Id == request.UserId && u.TenantId == _tenant.TenantId, ct);
        if (!userExists)
            return BadRequest(ApiResponse<CompensationDto>.Fail("User not found in this tenant."));
        if (request.Amount < 0)
            return BadRequest(ApiResponse<CompensationDto>.Fail("Amount must be non-negative."));

        var c = new EmployeeCompensation
        {
            TenantId = _tenant.TenantId.Value,
            UserId = request.UserId,
            Type = request.Type,
            Amount = request.Amount,
            PeriodLabel = request.PeriodLabel?.Trim(),
            PaidOn = request.PaidOn,
            Notes = request.Notes?.Trim()
        };
        _db.EmployeeCompensations.Add(c);
        await _db.SaveChangesAsync(ct);
        var created = await _db.EmployeeCompensations.AsNoTracking()
            .Include(x => x.User)
            .FirstAsync(x => x.Id == c.Id, ct);
        return CreatedAtAction(nameof(GetById), new { id = c.Id }, ApiResponse<CompensationDto>.Ok(new CompensationDto
        {
            Id = created.Id.ToString("D"),
            UserId = created.UserId.ToString("D"),
            UserName = created.User == null ? null : $"{created.User.FirstName} {created.User.LastName}".Trim(),
            Type = created.Type,
            Amount = created.Amount,
            PeriodLabel = created.PeriodLabel,
            PaidOn = created.PaidOn,
            Notes = created.Notes,
            CreatedAt = created.CreatedAt
        }));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<ActionResult<ApiResponse<CompensationDto>>> Update([FromRoute] Guid id, [FromBody] UpdateCompensationRequestDto request, CancellationToken ct = default)
    {
        var moduleCheck = await EnsureEmployeeManagementModuleAsync(ct);
        if (moduleCheck != null) return moduleCheck;
        if (!_tenant.TenantId.HasValue)
            return BadRequest(ApiResponse<CompensationDto>.Fail("Tenant context is missing."));
        var c = await _db.EmployeeCompensations.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _tenant.TenantId, ct);
        if (c is null) return NotFound(ApiResponse<CompensationDto>.Fail("Record not found."));
        if (request.Amount < 0)
            return BadRequest(ApiResponse<CompensationDto>.Fail("Amount must be non-negative."));

        c.Type = request.Type;
        c.Amount = request.Amount;
        c.PeriodLabel = request.PeriodLabel?.Trim();
        c.PaidOn = request.PaidOn;
        c.Notes = request.Notes?.Trim();
        await _db.SaveChangesAsync(ct);
        await _db.Entry(c).Reference(x => x.User).LoadAsync(ct);
        return ApiResponse<CompensationDto>.Ok(new CompensationDto
        {
            Id = c.Id.ToString("D"),
            UserId = c.UserId.ToString("D"),
            UserName = c.User == null ? null : $"{c.User.FirstName} {c.User.LastName}".Trim(),
            Type = c.Type,
            Amount = c.Amount,
            PeriodLabel = c.PeriodLabel,
            PaidOn = c.PaidOn,
            Notes = c.Notes,
            CreatedAt = c.CreatedAt
        });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<ActionResult<ApiResponse<object>>> Delete([FromRoute] Guid id, CancellationToken ct = default)
    {
        var moduleCheck = await EnsureEmployeeManagementModuleAsync(ct);
        if (moduleCheck != null) return moduleCheck;
        if (!_tenant.TenantId.HasValue)
            return BadRequest(ApiResponse<object>.Fail("Tenant context is missing."));
        var c = await _db.EmployeeCompensations.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _tenant.TenantId, ct);
        if (c is null) return NotFound(ApiResponse<object>.Fail("Record not found."));
        c.IsDeleted = true;
        c.DeletedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(new { });
    }
}
