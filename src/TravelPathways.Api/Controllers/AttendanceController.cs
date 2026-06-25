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
        if (EmployeeModuleAccess.IsEmployeeModuleEnabled(enabled))
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

    public sealed class ManualAttendanceRequestDto
    {
        public Guid UserId { get; set; }
        public DateTime AttendanceDate { get; set; }
        /// <summary>Time in as HH:mm (IST).</summary>
        public string? TimeIn { get; set; }
        /// <summary>Time out as HH:mm (IST).</summary>
        public string? TimeOut { get; set; }
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
        var effectiveDate = DateTimeUtcHelper.ToUtcDate(date ?? DateTime.UtcNow);
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
            fromDate = DateTimeUtcHelper.ToUtcDate(from.Value);
            toDate = DateTimeUtcHelper.ToUtcDate(to.Value);
            if (fromDate > toDate) (fromDate, toDate) = (toDate, fromDate);
        }
        else
        {
            var today = DateTimeUtcHelper.UtcToday();
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
        var today = DateTimeUtcHelper.UtcToday();
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
        var today = DateTimeUtcHelper.UtcToday();
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
            fromDate = DateTimeUtcHelper.ToUtcDate(date.Value);
            toDate = fromDate;
        }
        else if (from.HasValue && to.HasValue)
        {
            fromDate = DateTimeUtcHelper.ToUtcDate(from.Value);
            toDate = DateTimeUtcHelper.ToUtcDate(to.Value);
            if (fromDate > toDate) (fromDate, toDate) = (toDate, fromDate);
        }
        else
        {
            var today = DateTimeUtcHelper.UtcToday();
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

    /// <summary>Admin/HR: manually add or update attendance for any employee on a given date.</summary>
    [HttpPost("manual")]
    public async Task<ActionResult<ApiResponse<AttendanceDto>>> UpsertManualAttendance(
        [FromBody] ManualAttendanceRequestDto request,
        CancellationToken ct = default)
    {
        var check = await EnsureEmployeeModuleAsync(ct);
        if (check != null) return check;
        if (!IsTenantAdmin(User))
            return Forbid();

        if (request.UserId == Guid.Empty)
            return BadRequest(ApiResponse<AttendanceDto>.Fail("Employee is required."));
        if (string.IsNullOrWhiteSpace(request.TimeIn) && string.IsNullOrWhiteSpace(request.TimeOut))
            return BadRequest(ApiResponse<AttendanceDto>.Fail("Provide at least Time In or Time Out."));

        var attendanceDate = DateTimeUtcHelper.ToUtcDate(request.AttendanceDate);
        var userExists = await _db.Users.AsNoTracking()
            .AnyAsync(u => u.TenantId == TenantId && u.Id == request.UserId, ct);
        if (!userExists)
            return BadRequest(ApiResponse<AttendanceDto>.Fail("Employee not found."));

        DateTime? timeInUtc = null;
        DateTime? timeOutUtc = null;
        if (!string.IsNullOrWhiteSpace(request.TimeIn))
        {
            if (!TryParseIstTime(attendanceDate, request.TimeIn, out var parsedIn))
                return BadRequest(ApiResponse<AttendanceDto>.Fail("Time In must be in HH:mm format."));
            timeInUtc = parsedIn;
        }
        if (!string.IsNullOrWhiteSpace(request.TimeOut))
        {
            if (!TryParseIstTime(attendanceDate, request.TimeOut, out var parsedOut))
                return BadRequest(ApiResponse<AttendanceDto>.Fail("Time Out must be in HH:mm format."));
            timeOutUtc = parsedOut;
        }
        if (timeInUtc.HasValue && timeOutUtc.HasValue && timeOutUtc <= timeInUtc)
            return BadRequest(ApiResponse<AttendanceDto>.Fail("Time Out must be after Time In."));

        var existing = await _db.Attendances
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.TenantId == TenantId && a.UserId == request.UserId && a.AttendanceDate == attendanceDate, ct);

        if (existing != null)
        {
            if (timeInUtc.HasValue) existing.TimeInUtc = timeInUtc;
            if (timeOutUtc.HasValue) existing.TimeOutUtc = timeOutUtc;
            await _db.SaveChangesAsync(ct);
            return ApiResponse<AttendanceDto>.Ok(ToDto(existing), "Attendance updated.");
        }

        var att = new Attendance
        {
            TenantId = TenantId,
            UserId = request.UserId,
            AttendanceDate = attendanceDate,
            TimeInUtc = timeInUtc,
            TimeOutUtc = timeOutUtc
        };
        _db.Attendances.Add(att);
        await _db.SaveChangesAsync(ct);
        await _db.Entry(att).Reference(a => a.User).LoadAsync(ct);
        return ApiResponse<AttendanceDto>.Ok(ToDto(att), "Attendance added.");
    }

    /// <summary>Admin/HR: delete an attendance record.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteAttendance(
        [FromRoute] Guid id,
        CancellationToken ct = default)
    {
        var check = await EnsureEmployeeModuleAsync(ct);
        if (check != null) return check;
        if (!IsTenantAdmin(User))
            return Forbid();

        var att = await _db.Attendances
            .FirstOrDefaultAsync(a => a.Id == id && a.TenantId == TenantId, ct);
        if (att is null)
            return NotFound(ApiResponse<object>.Fail("Attendance record not found."));

        att.IsDeleted = true;
        att.DeletedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(new { }, "Attendance deleted.");
    }

    private static AttendanceDto ToDto(Attendance a) => new()
    {
        Id = a.Id.ToString("D"),
        UserId = a.UserId.ToString("D"),
        UserName = a.User == null ? null : $"{a.User.FirstName} {a.User.LastName}".Trim(),
        AttendanceDate = a.AttendanceDate,
        TimeInUtc = a.TimeInUtc,
        TimeOutUtc = a.TimeOutUtc
    };

    private static bool TryParseIstTime(DateTime attendanceDate, string timeHHmm, out DateTime utc)
    {
        utc = default;
        if (!TimeOnly.TryParse(timeHHmm.Trim(), out var time))
            return false;
        var ist = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "India Standard Time" : "Asia/Kolkata");
        var local = new DateTime(attendanceDate.Year, attendanceDate.Month, attendanceDate.Day,
            time.Hour, time.Minute, 0, DateTimeKind.Unspecified);
        utc = DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeToUtc(local, ist), DateTimeKind.Utc);
        return true;
    }
}
