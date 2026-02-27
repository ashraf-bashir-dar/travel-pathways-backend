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
[Route("api/attendance")]
public sealed class AttendanceController : TenantControllerBase
{
    private readonly AppDbContext _db;
    private readonly TenantContext _tenant;

    public AttendanceController(AppDbContext db, TenantContext tenant) : base(tenant)
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

    public sealed class AttendanceDto
    {
        public required string Id { get; init; }
        public required string UserId { get; init; }
        public string? UserName { get; init; }
        public required DateTime AttendanceDate { get; init; }
        public DateTime? TimeInUtc { get; init; }
        public DateTime? TimeOutUtc { get; init; }
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

    /// <summary>Get my attendance for a date. Returns single record if exists.</summary>
    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<AttendanceDto?>>> GetMyAttendance(
        [FromQuery] DateTime? date = null,
        CancellationToken ct = default)
    {
        var check = await EnsureEmployeeModuleAsync(ct);
        if (check != null) return check;
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
            return Unauthorized(ApiResponse<AttendanceDto?>.Fail("User not identified."));
        var effectiveDate = (date?.Date ?? DateTime.UtcNow.Date);
        var att = await _db.Attendances.AsNoTracking()
            .Where(a => a.TenantId == TenantId && a.UserId == currentUserId.Value && a.AttendanceDate == effectiveDate)
            .Include(a => a.User)
            .FirstOrDefaultAsync(ct);
        if (att == null)
            return ApiResponse<AttendanceDto?>.Ok(null);
        return ApiResponse<AttendanceDto?>.Ok(new AttendanceDto
        {
            Id = att.Id.ToString("D"),
            UserId = att.UserId.ToString("D"),
            UserName = att.User == null ? null : $"{att.User.FirstName} {att.User.LastName}".Trim(),
            AttendanceDate = att.AttendanceDate,
            TimeInUtc = att.TimeInUtc,
            TimeOutUtc = att.TimeOutUtc
        });
    }

    /// <summary>Get attendance over a date range.
    /// - Employee: when userId is omitted, returns their own records.
    /// - Admin: can optionally set userId to see another employee's records.
    /// </summary>
    [HttpGet("range")]
    public async Task<ActionResult<ApiResponse<List<AttendanceDto>>>> GetAttendanceRange(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] Guid? userId = null,
        CancellationToken ct = default)
    {
        var check = await EnsureEmployeeModuleAsync(ct);
        if (check != null) return check;
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
            return Unauthorized(ApiResponse<List<AttendanceDto>>.Fail("User not identified."));

        DateTime fromDate;
        DateTime toDate;
        if (from.HasValue && to.HasValue)
        {
            fromDate = from.Value.Date;
            toDate = to.Value.Date;
            if (fromDate > toDate) (fromDate, toDate) = (toDate, fromDate);
        }
        else
        {
            var today = DateTime.UtcNow.Date;
            fromDate = today.AddDays(-6);
            toDate = today;
        }

        var isAdmin = IsTenantAdmin(User);

        var query = _db.Attendances.AsNoTracking()
            .Where(a => a.TenantId == TenantId &&
                        a.AttendanceDate >= fromDate &&
                        a.AttendanceDate <= toDate);

        if (isAdmin)
        {
            // Admin: can see all employees by default, or a specific employee when userId is provided.
            if (userId.HasValue)
            {
                query = query.Where(a => a.UserId == userId.Value);
            }
        }
        else
        {
            // Non-admin (employee): always restricted to own attendance, ignore userId filter.
            query = query.Where(a => a.UserId == currentUserId.Value);
        }

        var list = await query
            .Include(a => a.User)
            .OrderBy(a => a.AttendanceDate)
            .ToListAsync(ct);

        var items = list.Select(a => new AttendanceDto
        {
            Id = a.Id.ToString("D"),
            UserId = a.UserId.ToString("D"),
            UserName = a.User == null ? null : $"{a.User.FirstName} {a.User.LastName}".Trim(),
            AttendanceDate = a.AttendanceDate,
            TimeInUtc = a.TimeInUtc,
            TimeOutUtc = a.TimeOutUtc
        }).ToList();

        return ApiResponse<List<AttendanceDto>>.Ok(items);
    }

    /// <summary>Mark Time In for today. One per day.</summary>
    [HttpPost("time-in")]
    public async Task<ActionResult<ApiResponse<AttendanceDto>>> MarkTimeIn(CancellationToken ct = default)
    {
        var check = await EnsureEmployeeModuleAsync(ct);
        if (check != null) return check;
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
            return Unauthorized(ApiResponse<AttendanceDto>.Fail("User not identified."));
        var today = DateTime.UtcNow.Date;
        var existing = await _db.Attendances
            .FirstOrDefaultAsync(a => a.TenantId == TenantId && a.UserId == currentUserId.Value && a.AttendanceDate == today, ct);
        var now = DateTime.UtcNow;
        if (existing != null)
        {
            if (existing.TimeInUtc.HasValue)
                return BadRequest(ApiResponse<AttendanceDto>.Fail("Time In already marked for today."));
            existing.TimeInUtc = now;
            await _db.SaveChangesAsync(ct);
            return ApiResponse<AttendanceDto>.Ok(new AttendanceDto
            {
                Id = existing.Id.ToString("D"),
                UserId = existing.UserId.ToString("D"),
                UserName = null,
                AttendanceDate = existing.AttendanceDate,
                TimeInUtc = existing.TimeInUtc,
                TimeOutUtc = existing.TimeOutUtc
            });
        }
        var att = new Attendance
        {
            TenantId = TenantId,
            UserId = currentUserId.Value,
            AttendanceDate = today,
            TimeInUtc = now
        };
        _db.Attendances.Add(att);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<AttendanceDto>.Ok(new AttendanceDto
        {
            Id = att.Id.ToString("D"),
            UserId = att.UserId.ToString("D"),
            UserName = null,
            AttendanceDate = att.AttendanceDate,
            TimeInUtc = att.TimeInUtc,
            TimeOutUtc = att.TimeOutUtc
        });
    }

    /// <summary>Mark Time Out for today. Requires Time In first; one per day.</summary>
    [HttpPost("time-out")]
    public async Task<ActionResult<ApiResponse<AttendanceDto>>> MarkTimeOut(CancellationToken ct = default)
    {
        var check = await EnsureEmployeeModuleAsync(ct);
        if (check != null) return check;
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
            return Unauthorized(ApiResponse<AttendanceDto>.Fail("User not identified."));
        var today = DateTime.UtcNow.Date;
        var existing = await _db.Attendances
            .FirstOrDefaultAsync(a => a.TenantId == TenantId && a.UserId == currentUserId.Value && a.AttendanceDate == today, ct);
        if (existing == null)
            return BadRequest(ApiResponse<AttendanceDto>.Fail("Mark Time In first before Time Out."));
        if (!existing.TimeInUtc.HasValue)
            return BadRequest(ApiResponse<AttendanceDto>.Fail("Time In not found for today. Mark Time In first."));
        if (existing.TimeOutUtc.HasValue)
            return BadRequest(ApiResponse<AttendanceDto>.Fail("Time Out already marked for today."));
        existing.TimeOutUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<AttendanceDto>.Ok(new AttendanceDto
        {
            Id = existing.Id.ToString("D"),
            UserId = existing.UserId.ToString("D"),
            UserName = null,
            AttendanceDate = existing.AttendanceDate,
            TimeInUtc = existing.TimeInUtc,
            TimeOutUtc = existing.TimeOutUtc
        });
    }

    /// <summary>Admin: list attendance for a date or range, optional user filter.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<AttendanceDto>>>> GetAttendance(
        [FromQuery] DateTime? date = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] Guid? userId = null,
        CancellationToken ct = default)
    {
        var check = await EnsureEmployeeModuleAsync(ct);
        if (check != null) return check;
        if (!IsTenantAdmin(User))
            return Forbid();
        DateTime fromDate;
        DateTime toDate;
        if (date.HasValue)
        {
            fromDate = date.Value.Date;
            toDate = fromDate;
        }
        else if (from.HasValue && to.HasValue)
        {
            fromDate = from.Value.Date;
            toDate = to.Value.Date;
            if (fromDate > toDate) (fromDate, toDate) = (toDate, fromDate);
        }
        else
        {
            var today = DateTime.UtcNow.Date;
            fromDate = today;
            toDate = today;
        }
        var query = _db.Attendances.AsNoTracking()
            .Where(a => a.TenantId == TenantId && a.AttendanceDate >= fromDate && a.AttendanceDate <= toDate);
        if (userId.HasValue)
            query = query.Where(a => a.UserId == userId.Value);
        var list = await query
            .Include(a => a.User)
            .OrderBy(a => a.AttendanceDate)
            .ThenBy(a => a.User!.FirstName)
            .ToListAsync(ct);
        var items = list.Select(a => new AttendanceDto
        {
            Id = a.Id.ToString("D"),
            UserId = a.UserId.ToString("D"),
            UserName = a.User == null ? null : $"{a.User.FirstName} {a.User.LastName}".Trim(),
            AttendanceDate = a.AttendanceDate,
            TimeInUtc = a.TimeInUtc,
            TimeOutUtc = a.TimeOutUtc
        }).ToList();
        return ApiResponse<List<AttendanceDto>>.Ok(items);
    }
}
