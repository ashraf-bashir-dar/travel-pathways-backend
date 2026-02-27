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
[Route("api/employee-monitoring")]
public sealed class EmployeeMonitoringController : TenantControllerBase
{
    private readonly AppDbContext _db;
    private readonly TenantContext _tenant;

    public EmployeeMonitoringController(AppDbContext db, TenantContext tenant) : base(tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    /// <summary>Ensure tenant has TimeSheet or Employee Management module. Null/empty EnabledModules = allow.</summary>
    private async Task<ActionResult?> EnsureTimeSheetOrEmployeeManagementModuleAsync(CancellationToken ct)
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
        return StatusCode(403, ApiResponse<object>.Fail("TimeSheet or Employee Management module is not enabled for this tenant."));
    }

    public sealed class DailyTaskDto
    {
        public required string Id { get; init; }
        public required string UserId { get; init; }
        public string? UserName { get; init; }
        public required DateTime TaskDate { get; init; }
        public required string Description { get; init; }
        public int DisplayOrder { get; init; }
        public required DateTime CreatedAt { get; init; }
        public DateTime? StartTimeUtc { get; init; }
        public DateTime? EndTimeUtc { get; init; }
    }

    public sealed class CreateDailyTaskRequestDto
    {
        public DateTime TaskDate { get; set; }
        public string Description { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public DateTime? StartTimeUtc { get; set; }
        public DateTime? EndTimeUtc { get; set; }
    }

    public sealed class UpdateDailyTaskRequestDto
    {
        public string Description { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public DateTime? StartTimeUtc { get; set; }
        public DateTime? EndTimeUtc { get; set; }
    }

    private const double MaxTaskDurationHours = 2.0;

    private Guid? GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    private bool IsTenantAdmin()
    {
        var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
        return string.Equals(role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Get tasks for a date. Optional userId: if provided, Admin can see that user's tasks; otherwise returns current user's tasks.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<DailyTaskDto>>>> GetTasks(
        [FromQuery] DateTime? date = null,
        [FromQuery] Guid? userId = null,
        CancellationToken ct = default)
    {
        var check = await EnsureTimeSheetOrEmployeeManagementModuleAsync(ct);
        if (check != null) return check;
        var effectiveDate = date?.Date ?? DateTime.UtcNow.Date;
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
            return Unauthorized(ApiResponse<List<DailyTaskDto>>.Fail("User not identified."));

        Guid filterUserId;
        if (userId.HasValue)
        {
            if (!IsTenantAdmin())
                return Forbid();
            filterUserId = userId.Value;
        }
        else
        {
            filterUserId = currentUserId.Value;
        }

        var tasks = await _db.EmployeeDailyTasks.AsNoTracking()
            .Where(t => t.TenantId == TenantId && t.TaskDate.Date == effectiveDate && t.UserId == filterUserId)
            .Include(t => t.User)
            .OrderBy(t => t.DisplayOrder)
            .ThenBy(t => t.CreatedAt)
            .ToListAsync(ct);

        var items = tasks.Select(t => new DailyTaskDto
        {
            Id = t.Id.ToString("D"),
            UserId = t.UserId.ToString("D"),
            UserName = t.User == null ? null : $"{t.User.FirstName} {t.User.LastName}".Trim(),
            TaskDate = t.TaskDate,
            Description = t.Description,
            DisplayOrder = t.DisplayOrder,
            CreatedAt = t.CreatedAt,
            StartTimeUtc = t.StartTimeUtc,
            EndTimeUtc = t.EndTimeUtc
        }).ToList();

        return ApiResponse<List<DailyTaskDto>>.Ok(items);
    }

    /// <summary>Get tasks for a full month. Optional userId: if provided, Admin can see that user's tasks; otherwise current user's. Use for "what I did this month" view.</summary>
    [HttpGet("month")]
    public async Task<ActionResult<ApiResponse<List<DailyTaskDto>>>> GetTasksForMonth(
        [FromQuery] int year,
        [FromQuery] int month,
        [FromQuery] Guid? userId = null,
        CancellationToken ct = default)
    {
        var check = await EnsureTimeSheetOrEmployeeManagementModuleAsync(ct);
        if (check != null) return check;
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
            return Unauthorized(ApiResponse<List<DailyTaskDto>>.Fail("User not identified."));

        if (year < 2000 || year > 2100 || month < 1 || month > 12)
            return BadRequest(ApiResponse<List<DailyTaskDto>>.Fail("Invalid year or month."));

        Guid filterUserId;
        if (userId.HasValue)
        {
            if (!IsTenantAdmin())
                return Forbid();
            filterUserId = userId.Value;
        }
        else
        {
            filterUserId = currentUserId.Value;
        }

        var start = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMonths(1).AddDays(-1);

        var tasks = await _db.EmployeeDailyTasks.AsNoTracking()
            .Where(t => t.TenantId == TenantId && t.UserId == filterUserId && t.TaskDate.Date >= start && t.TaskDate.Date <= end)
            .Include(t => t.User)
            .OrderBy(t => t.TaskDate)
            .ThenBy(t => t.DisplayOrder)
            .ThenBy(t => t.CreatedAt)
            .ToListAsync(ct);

        var items = tasks.Select(t => new DailyTaskDto
        {
            Id = t.Id.ToString("D"),
            UserId = t.UserId.ToString("D"),
            UserName = t.User == null ? null : $"{t.User.FirstName} {t.User.LastName}".Trim(),
            TaskDate = t.TaskDate,
            Description = t.Description,
            DisplayOrder = t.DisplayOrder,
            CreatedAt = t.CreatedAt,
            StartTimeUtc = t.StartTimeUtc,
            EndTimeUtc = t.EndTimeUtc
        }).ToList();

        return ApiResponse<List<DailyTaskDto>>.Ok(items);
    }

    /// <summary>Tenant admin only: get all users' tasks for a date (or date range). Read-only.</summary>
    [HttpGet("all")]
    public async Task<ActionResult<ApiResponse<List<DailyTaskDto>>>> GetAllTasks(
        [FromQuery] DateTime? date = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] Guid? userId = null,
        CancellationToken ct = default)
    {
        var check = await EnsureTimeSheetOrEmployeeManagementModuleAsync(ct);
        if (check != null) return check;
        if (!IsTenantAdmin())
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

        var query = _db.EmployeeDailyTasks.AsNoTracking()
            .Where(t => t.TenantId == TenantId && t.TaskDate.Date >= fromDate && t.TaskDate.Date <= toDate);
        if (userId.HasValue)
            query = query.Where(t => t.UserId == userId.Value);
        var tasks = await query
            .Include(t => t.User)
            .OrderBy(t => t.TaskDate)
            .ThenBy(t => t.User!.FirstName)
            .ThenBy(t => t.DisplayOrder)
            .ThenBy(t => t.CreatedAt)
            .ToListAsync(ct);

        var items = tasks.Select(t => new DailyTaskDto
        {
            Id = t.Id.ToString("D"),
            UserId = t.UserId.ToString("D"),
            UserName = t.User == null ? null : $"{t.User.FirstName} {t.User.LastName}".Trim(),
            TaskDate = t.TaskDate,
            Description = t.Description,
            DisplayOrder = t.DisplayOrder,
            CreatedAt = t.CreatedAt,
            StartTimeUtc = t.StartTimeUtc,
            EndTimeUtc = t.EndTimeUtc
        }).ToList();

        return ApiResponse<List<DailyTaskDto>>.Ok(items);
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<DailyTaskDto>>> CreateTask([FromBody] CreateDailyTaskRequestDto request, CancellationToken ct = default)
    {
        var check = await EnsureTimeSheetOrEmployeeManagementModuleAsync(ct);
        if (check != null) return check;
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
            return Unauthorized(ApiResponse<DailyTaskDto>.Fail("User not identified."));
        if (string.IsNullOrWhiteSpace(request.Description?.Trim()))
            return BadRequest(ApiResponse<DailyTaskDto>.Fail("Description is required."));

        var taskDate = request.TaskDate.Date;
        DateTime? startUtc = request.StartTimeUtc.HasValue ? DateTime.SpecifyKind(request.StartTimeUtc.Value, DateTimeKind.Utc) : null;
        DateTime? endUtc = request.EndTimeUtc.HasValue ? DateTime.SpecifyKind(request.EndTimeUtc.Value, DateTimeKind.Utc) : null;
        if (startUtc.HasValue || endUtc.HasValue)
        {
            if (!startUtc.HasValue || !endUtc.HasValue)
                return BadRequest(ApiResponse<DailyTaskDto>.Fail("Both start time and end time are required for a task."));
            if (startUtc.Value.Date != taskDate || endUtc.Value.Date != taskDate)
                return BadRequest(ApiResponse<DailyTaskDto>.Fail("Start and end time must be on the same date as the task."));
            if (endUtc.Value <= startUtc.Value)
                return BadRequest(ApiResponse<DailyTaskDto>.Fail("End time must be after start time."));
            var durationHours = (endUtc.Value - startUtc.Value).TotalHours;
            if (durationHours > MaxTaskDurationHours)
                return BadRequest(ApiResponse<DailyTaskDto>.Fail($"Task duration cannot exceed {MaxTaskDurationHours} hours."));
        }

        var task = new EmployeeDailyTask
        {
            TenantId = TenantId,
            UserId = currentUserId.Value,
            TaskDate = taskDate,
            Description = request.Description.Trim(),
            DisplayOrder = request.DisplayOrder,
            StartTimeUtc = startUtc,
            EndTimeUtc = endUtc
        };
        _db.EmployeeDailyTasks.Add(task);
        await _db.SaveChangesAsync(ct);

        var dto = new DailyTaskDto
        {
            Id = task.Id.ToString("D"),
            UserId = task.UserId.ToString("D"),
            UserName = null,
            TaskDate = task.TaskDate,
            Description = task.Description,
            DisplayOrder = task.DisplayOrder,
            CreatedAt = task.CreatedAt,
            StartTimeUtc = task.StartTimeUtc,
            EndTimeUtc = task.EndTimeUtc
        };
        return CreatedAtAction(nameof(GetTasks), new { date = taskDate.ToString("yyyy-MM-dd") }, ApiResponse<DailyTaskDto>.Ok(dto));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<DailyTaskDto>>> UpdateTask([FromRoute] Guid id, [FromBody] UpdateDailyTaskRequestDto request, CancellationToken ct = default)
    {
        var check = await EnsureTimeSheetOrEmployeeManagementModuleAsync(ct);
        if (check != null) return check;
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
            return Unauthorized(ApiResponse<DailyTaskDto>.Fail("User not identified."));

        var task = await _db.EmployeeDailyTasks.FirstOrDefaultAsync(t => t.Id == id && t.TenantId == TenantId, ct);
        if (task is null)
            return NotFound(ApiResponse<DailyTaskDto>.Fail("Task not found."));
        if (task.UserId != currentUserId.Value)
            return Forbid();
        if (string.IsNullOrWhiteSpace(request.Description?.Trim()))
            return BadRequest(ApiResponse<DailyTaskDto>.Fail("Description is required."));

        DateTime? startUtc = request.StartTimeUtc.HasValue ? DateTime.SpecifyKind(request.StartTimeUtc.Value, DateTimeKind.Utc) : null;
        DateTime? endUtc = request.EndTimeUtc.HasValue ? DateTime.SpecifyKind(request.EndTimeUtc.Value, DateTimeKind.Utc) : null;
        if (startUtc.HasValue || endUtc.HasValue)
        {
            if (!startUtc.HasValue || !endUtc.HasValue)
                return BadRequest(ApiResponse<DailyTaskDto>.Fail("Both start time and end time are required for a task."));
            var taskDate = task.TaskDate.Date;
            if (startUtc.Value.Date != taskDate || endUtc.Value.Date != taskDate)
                return BadRequest(ApiResponse<DailyTaskDto>.Fail("Start and end time must be on the same date as the task."));
            if (endUtc.Value <= startUtc.Value)
                return BadRequest(ApiResponse<DailyTaskDto>.Fail("End time must be after start time."));
            var durationHours = (endUtc.Value - startUtc.Value).TotalHours;
            if (durationHours > MaxTaskDurationHours)
                return BadRequest(ApiResponse<DailyTaskDto>.Fail($"Task duration cannot exceed {MaxTaskDurationHours} hours."));
        }

        task.Description = request.Description.Trim();
        task.DisplayOrder = request.DisplayOrder;
        task.StartTimeUtc = startUtc;
        task.EndTimeUtc = endUtc;
        await _db.SaveChangesAsync(ct);

        return ApiResponse<DailyTaskDto>.Ok(new DailyTaskDto
        {
            Id = task.Id.ToString("D"),
            UserId = task.UserId.ToString("D"),
            UserName = null,
            TaskDate = task.TaskDate,
            Description = task.Description,
            DisplayOrder = task.DisplayOrder,
            CreatedAt = task.CreatedAt,
            StartTimeUtc = task.StartTimeUtc,
            EndTimeUtc = task.EndTimeUtc
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteTask([FromRoute] Guid id, CancellationToken ct = default)
    {
        var check = await EnsureTimeSheetOrEmployeeManagementModuleAsync(ct);
        if (check != null) return check;
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
            return Unauthorized(ApiResponse<object>.Fail("User not identified."));

        var task = await _db.EmployeeDailyTasks.FirstOrDefaultAsync(t => t.Id == id && t.TenantId == TenantId, ct);
        if (task is null)
            return NotFound(ApiResponse<object>.Fail("Task not found."));
        if (task.UserId != currentUserId.Value)
            return Forbid();

        task.IsDeleted = true;
        task.DeletedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(new { });
    }
}
