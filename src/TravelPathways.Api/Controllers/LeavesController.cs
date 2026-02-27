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
[Route("api/leaves")]
public sealed class LeavesController : TenantControllerBase
{
    private readonly AppDbContext _db;
    private readonly TenantContext _tenant;

    public LeavesController(AppDbContext db, TenantContext tenant) : base(tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private async Task<ActionResult?> EnsureEmployeeModuleAsync(CancellationToken ct)
    {
        if (_tenant.IsSuperAdmin) return null;
        if (!_tenant.TenantId.HasValue) return BadRequest(ApiResponse<object>.Fail("Tenant context is missing."));
        var enabled = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == _tenant.TenantId.Value)
            .Select(t => t.EnabledModules)
            .FirstOrDefaultAsync(ct);
        if (enabled == null || enabled.Count == 0) return null;
        if (enabled.Contains(AppModuleKey.TimeSheet) || enabled.Contains(AppModuleKey.EmployeeManagement) || enabled.Contains(AppModuleKey.EmployeeMonitoring))
            return null;
        return StatusCode(403, ApiResponse<object>.Fail("Employee module is not enabled for this tenant."));
    }

    public sealed class LeaveDto
    {
        public required string Id { get; init; }
        public required string UserId { get; init; }
        public string? UserName { get; init; }
        public required string LeaveType { get; init; }
        public required DateTime StartDate { get; init; }
        public required DateTime EndDate { get; init; }
        public required string Reason { get; init; }
        public required string Status { get; init; }
        public required DateTime CreatedAt { get; init; }
    }

    public sealed class ApplyLeaveRequestDto
    {
        public LeaveType LeaveType { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    private Guid? GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    private static bool IsTenantAdmin(ClaimsPrincipal user)
    {
        var role = user.FindFirstValue(ClaimTypes.Role) ?? user.FindFirstValue("role");
        return string.Equals(role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static LeaveDto ToDto(Leave l) => new LeaveDto
    {
        Id = l.Id.ToString("D"),
        UserId = l.UserId.ToString("D"),
        UserName = l.User == null ? null : $"{l.User.FirstName} {l.User.LastName}".Trim(),
        LeaveType = l.LeaveType.ToString(),
        StartDate = l.StartDate,
        EndDate = l.EndDate,
        Reason = l.Reason,
        Status = l.Status.ToString(),
        CreatedAt = l.CreatedAt
    };

    /// <summary>Employee: apply for leave.</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<LeaveDto>>> ApplyLeave([FromBody] ApplyLeaveRequestDto request, CancellationToken ct = default)
    {
        var check = await EnsureEmployeeModuleAsync(ct);
        if (check != null) return check;
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
            return Unauthorized(ApiResponse<LeaveDto>.Fail("User not identified."));
        var start = request.StartDate.Date;
        var end = request.EndDate.Date;
        if (start > end)
            return BadRequest(ApiResponse<LeaveDto>.Fail("Start date must be on or before end date."));
        if (string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(ApiResponse<LeaveDto>.Fail("Reason is required."));

        var leave = new Leave
        {
            TenantId = TenantId,
            UserId = currentUserId.Value,
            LeaveType = request.LeaveType,
            StartDate = start,
            EndDate = end,
            Reason = request.Reason.Trim(),
            Status = LeaveStatus.Pending
        };
        _db.Leaves.Add(leave);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<LeaveDto>.Ok(ToDto(leave));
    }

    /// <summary>Get leaves: my leaves (employee) or all with filters (admin).</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<LeaveDto>>>> GetLeaves(
        [FromQuery] Guid? userId = null,
        [FromQuery] LeaveStatus? status = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        var check = await EnsureEmployeeModuleAsync(ct);
        if (check != null) return check;
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
            return Unauthorized(ApiResponse<List<LeaveDto>>.Fail("User not identified."));

        var isAdmin = IsTenantAdmin(User);

        var query = _db.Leaves.AsNoTracking()
            .Where(l => l.TenantId == TenantId);

        if (isAdmin)
        {
            // Admin: see all leaves by default, or a specific employee when userId is provided.
            if (userId.HasValue)
            {
                query = query.Where(l => l.UserId == userId.Value);
            }
        }
        else
        {
            // Non-admin: always restricted to own leaves, ignore userId filter.
            query = query.Where(l => l.UserId == currentUserId.Value);
        }
        if (status.HasValue)
            query = query.Where(l => l.Status == status.Value);
        if (from.HasValue)
            query = query.Where(l => l.EndDate >= from.Value.Date);
        if (to.HasValue)
            query = query.Where(l => l.StartDate <= to.Value.Date);

        var list = await query
            .Include(l => l.User)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(ct);
        return ApiResponse<List<LeaveDto>>.Ok(list.Select(ToDto).ToList());
    }

    /// <summary>Admin: approve a leave request.</summary>
    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<ApiResponse<LeaveDto>>> ApproveLeave([FromRoute] Guid id, CancellationToken ct = default)
    {
        var check = await EnsureEmployeeModuleAsync(ct);
        if (check != null) return check;
        if (!IsTenantAdmin(User))
            return Forbid();
        var leave = await _db.Leaves.Include(l => l.User).FirstOrDefaultAsync(l => l.Id == id && l.TenantId == TenantId, ct);
        if (leave == null)
            return NotFound(ApiResponse<LeaveDto>.Fail("Leave not found."));
        if (leave.Status != LeaveStatus.Pending)
            return BadRequest(ApiResponse<LeaveDto>.Fail("Leave is already processed."));
        leave.Status = LeaveStatus.Approved;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<LeaveDto>.Ok(ToDto(leave));
    }

    /// <summary>Admin: reject a leave request.</summary>
    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<ApiResponse<LeaveDto>>> RejectLeave([FromRoute] Guid id, CancellationToken ct = default)
    {
        var check = await EnsureEmployeeModuleAsync(ct);
        if (check != null) return check;
        if (!IsTenantAdmin(User))
            return Forbid();
        var leave = await _db.Leaves.Include(l => l.User).FirstOrDefaultAsync(l => l.Id == id && l.TenantId == TenantId, ct);
        if (leave == null)
            return NotFound(ApiResponse<LeaveDto>.Fail("Leave not found."));
        if (leave.Status != LeaveStatus.Pending)
            return BadRequest(ApiResponse<LeaveDto>.Fail("Leave is already processed."));
        leave.Status = LeaveStatus.Rejected;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<LeaveDto>.Ok(ToDto(leave));
    }
}
