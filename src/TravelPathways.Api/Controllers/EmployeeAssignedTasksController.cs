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
[Route("api/employee-assigned-tasks")]
public sealed class EmployeeAssignedTasksController : TenantControllerBase
{
    private readonly AppDbContext _db;
    private readonly TenantContext _tenant;

    public EmployeeAssignedTasksController(AppDbContext db, TenantContext tenant) : base(tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public sealed class EmployeeAssignedTaskDto
    {
        public required string Id { get; init; }
        public required string Title { get; init; }
        public string? Description { get; init; }
        public required string DueDate { get; init; }
        public required string AssignedToUserId { get; init; }
        public string? AssignedToUserName { get; init; }
        public required string AssignedByUserId { get; init; }
        public string? AssignedByUserName { get; init; }
        public required string Status { get; init; }
        public DateTime? CompletedAtUtc { get; init; }
        public string? AssigneeNotes { get; init; }
        public required DateTime CreatedAt { get; init; }
        public required DateTime UpdatedAt { get; init; }
    }

    public sealed class CreateEmployeeAssignedTaskRequestDto
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        /// <summary>yyyy-MM-dd</summary>
        public string DueDate { get; set; } = string.Empty;
        public Guid AssignedToUserId { get; set; }
    }

    public sealed class BulkCreateEmployeeAssignedTaskRequestDto
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        /// <summary>yyyy-MM-dd</summary>
        public string DueDate { get; set; } = string.Empty;
    }

    public sealed class BulkCreateEmployeeAssignedTaskResultDto
    {
        public int CreatedCount { get; init; }
    }

    public sealed class UpdateEmployeeAssignedTaskRequestDto
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string DueDate { get; set; } = string.Empty;
        public Guid AssignedToUserId { get; set; }
        public EmployeeAssignedTaskStatus Status { get; set; }
    }

    public sealed class UpdateTaskProgressRequestDto
    {
        public string? AssigneeNotes { get; set; }
    }

    public sealed class CompleteEmployeeAssignedTaskRequestDto
    {
        public string? AssigneeNotes { get; set; }
    }

    private async Task<List<AppModuleKey>?> GetTenantEnabledModulesAsync(CancellationToken ct)
    {
        if (!_tenant.TenantId.HasValue) return null;
        return await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == _tenant.TenantId.Value)
            .Select(t => t.EnabledModules)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<ActionResult?> EnsureEmployeeModuleAsync(CancellationToken ct)
    {
        if (_tenant.IsSuperAdmin) return null;
        if (!_tenant.TenantId.HasValue) return BadRequest(ApiResponse<object>.Fail("Tenant context is missing."));
        var enabled = await GetTenantEnabledModulesAsync(ct);
        if (enabled == null || enabled.Count == 0) return null;
        if (EmployeeModuleAccess.IsEmployeeModuleEnabled(enabled))
            return null;
        return StatusCode(403, ApiResponse<object>.Fail("Employee module is not enabled for this tenant."));
    }

    private async Task<ActionResult?> EnsureTasksModuleAsync(CancellationToken ct)
    {
        if (_tenant.IsSuperAdmin) return null;
        if (!_tenant.TenantId.HasValue) return BadRequest(ApiResponse<object>.Fail("Tenant context is missing."));
        var enabled = await GetTenantEnabledModulesAsync(ct);
        if (enabled == null || enabled.Count == 0) return null;
        if (enabled.Contains(AppModuleKey.Tasks))
            return null;
        return StatusCode(403, ApiResponse<object>.Fail("Tasks module is not enabled for this tenant."));
    }

    private async Task<ActionResult?> EnsureCanViewAssignedTasksAsync(CancellationToken ct)
    {
        if (IsTenantAdmin()) return await EnsureEmployeeModuleAsync(ct);
        return await EnsureTasksModuleAsync(ct);
    }

    private Guid? GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    private static EmployeeAssignedTaskDto ToDto(EmployeeAssignedTask t) => new()
    {
        Id = t.Id.ToString("D"),
        Title = t.Title,
        Description = t.Description,
        DueDate = t.DueDate.ToString("yyyy-MM-dd"),
        AssignedToUserId = t.AssignedToUserId.ToString("D"),
        AssignedToUserName = t.AssignedToUser == null
            ? null
            : $"{t.AssignedToUser.FirstName} {t.AssignedToUser.LastName}".Trim(),
        AssignedByUserId = t.AssignedByUserId.ToString("D"),
        AssignedByUserName = t.AssignedByUser == null
            ? null
            : $"{t.AssignedByUser.FirstName} {t.AssignedByUser.LastName}".Trim(),
        Status = t.Status.ToString(),
        CompletedAtUtc = t.CompletedAtUtc,
        AssigneeNotes = t.AssigneeNotes,
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt
    };

    private async Task<bool> IsAssignableTenantUserAsync(Guid userId, CancellationToken ct) =>
        await _db.Users.AsNoTracking()
            .AnyAsync(u => u.Id == userId && u.TenantId == TenantId && u.IsActive, ct);

    private static DateOnly? ParseDueDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return DateOnly.TryParse(value.Trim(), out var d) ? d : null;
    }

    private static DateOnly BusinessToday()
    {
        foreach (var id in new[] { "Asia/Kolkata", "India Standard Time" })
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(id);
                return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz));
            }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }

        return DateOnly.FromDateTime(DateTime.UtcNow);
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<EmployeeAssignedTaskDto>>>> GetTasks(
        [FromQuery] Guid? assignedToUserId = null,
        [FromQuery] EmployeeAssignedTaskStatus? status = null,
        [FromQuery] DateOnly? dueFrom = null,
        [FromQuery] DateOnly? dueTo = null,
        [FromQuery] bool overdueOnly = false,
        CancellationToken ct = default)
    {
        var check = await EnsureCanViewAssignedTasksAsync(ct);
        if (check != null) return check;

        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
            return Unauthorized(ApiResponse<List<EmployeeAssignedTaskDto>>.Fail("User not identified."));

        var isAdmin = IsTenantAdmin();
        var query = _db.EmployeeAssignedTasks.AsNoTracking()
            .Where(t => t.TenantId == TenantId);

        if (isAdmin)
        {
            if (assignedToUserId.HasValue)
                query = query.Where(t => t.AssignedToUserId == assignedToUserId.Value);
        }
        else
        {
            query = query.Where(t => t.AssignedToUserId == currentUserId.Value);
        }

        if (status.HasValue)
            query = query.Where(t => t.Status == status.Value);
        if (dueFrom.HasValue)
            query = query.Where(t => t.DueDate >= dueFrom.Value);
        if (dueTo.HasValue)
            query = query.Where(t => t.DueDate <= dueTo.Value);
        if (overdueOnly)
        {
            var today = BusinessToday();
            query = query.Where(t =>
                t.Status == EmployeeAssignedTaskStatus.Pending && t.DueDate < today);
        }

        var list = await query
            .Include(t => t.AssignedToUser)
            .Include(t => t.AssignedByUser)
            .OrderBy(t => t.DueDate)
            .ThenByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

        return ApiResponse<List<EmployeeAssignedTaskDto>>.Ok(list.Select(ToDto).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<EmployeeAssignedTaskDto>>> CreateTask(
        [FromBody] CreateEmployeeAssignedTaskRequestDto request,
        CancellationToken ct = default)
    {
        var check = await EnsureEmployeeModuleAsync(ct);
        if (check != null) return check;

        var adminDenied = DenyUnlessTenantAdmin();
        if (adminDenied != null) return adminDenied;

        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
            return Unauthorized(ApiResponse<EmployeeAssignedTaskDto>.Fail("User not identified."));

        var title = request.Title?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(title))
            return BadRequest(ApiResponse<EmployeeAssignedTaskDto>.Fail("Title is required."));

        var dueDate = ParseDueDate(request.DueDate);
        if (!dueDate.HasValue)
            return BadRequest(ApiResponse<EmployeeAssignedTaskDto>.Fail("Due date is required (yyyy-MM-dd)."));

        if (!await IsAssignableTenantUserAsync(request.AssignedToUserId, ct))
            return BadRequest(ApiResponse<EmployeeAssignedTaskDto>.Fail("Assigned user not found or inactive."));

        var task = new EmployeeAssignedTask
        {
            TenantId = TenantId,
            Title = title,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            DueDate = dueDate.Value,
            AssignedToUserId = request.AssignedToUserId,
            AssignedByUserId = currentUserId.Value,
            Status = EmployeeAssignedTaskStatus.Pending
        };

        _db.EmployeeAssignedTasks.Add(task);
        await _db.SaveChangesAsync(ct);

        await _db.Entry(task).Reference(t => t.AssignedToUser).LoadAsync(ct);
        await _db.Entry(task).Reference(t => t.AssignedByUser).LoadAsync(ct);

        return CreatedAtAction(
            nameof(GetTasks),
            null,
            ApiResponse<EmployeeAssignedTaskDto>.Ok(ToDto(task)));
    }

    [HttpPost("bulk")]
    public async Task<ActionResult<ApiResponse<BulkCreateEmployeeAssignedTaskResultDto>>> CreateTasksBulk(
        [FromBody] BulkCreateEmployeeAssignedTaskRequestDto request,
        CancellationToken ct = default)
    {
        var check = await EnsureEmployeeModuleAsync(ct);
        if (check != null) return check;

        var adminDenied = DenyUnlessTenantAdmin();
        if (adminDenied != null) return adminDenied;

        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
            return Unauthorized(ApiResponse<BulkCreateEmployeeAssignedTaskResultDto>.Fail("User not identified."));

        var title = request.Title?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(title))
            return BadRequest(ApiResponse<BulkCreateEmployeeAssignedTaskResultDto>.Fail("Title is required."));

        var dueDate = ParseDueDate(request.DueDate);
        if (!dueDate.HasValue)
            return BadRequest(ApiResponse<BulkCreateEmployeeAssignedTaskResultDto>.Fail("Due date is required (yyyy-MM-dd)."));

        var assigneeIds = await _db.Users.AsNoTracking()
            .Where(u => u.TenantId == TenantId && u.IsActive)
            .Select(u => u.Id)
            .ToListAsync(ct);

        if (assigneeIds.Count == 0)
            return BadRequest(ApiResponse<BulkCreateEmployeeAssignedTaskResultDto>.Fail("No active employees found."));

        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        var now = DateTime.UtcNow;
        var tasks = assigneeIds.Select(userId => new EmployeeAssignedTask
        {
            TenantId = TenantId,
            Title = title,
            Description = description,
            DueDate = dueDate.Value,
            AssignedToUserId = userId,
            AssignedByUserId = currentUserId.Value,
            Status = EmployeeAssignedTaskStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        }).ToList();

        _db.EmployeeAssignedTasks.AddRange(tasks);
        await _db.SaveChangesAsync(ct);

        return ApiResponse<BulkCreateEmployeeAssignedTaskResultDto>.Ok(new BulkCreateEmployeeAssignedTaskResultDto
        {
            CreatedCount = tasks.Count
        });
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<EmployeeAssignedTaskDto>>> UpdateTask(
        [FromRoute] Guid id,
        [FromBody] UpdateEmployeeAssignedTaskRequestDto request,
        CancellationToken ct = default)
    {
        var check = await EnsureEmployeeModuleAsync(ct);
        if (check != null) return check;

        var adminDenied = DenyUnlessTenantAdmin();
        if (adminDenied != null) return adminDenied;

        var task = await _db.EmployeeAssignedTasks
            .Include(t => t.AssignedToUser)
            .Include(t => t.AssignedByUser)
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == TenantId, ct);
        if (task == null)
            return NotFound(ApiResponse<EmployeeAssignedTaskDto>.Fail("Task not found."));

        var title = request.Title?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(title))
            return BadRequest(ApiResponse<EmployeeAssignedTaskDto>.Fail("Title is required."));

        var dueDate = ParseDueDate(request.DueDate);
        if (!dueDate.HasValue)
            return BadRequest(ApiResponse<EmployeeAssignedTaskDto>.Fail("Due date is required (yyyy-MM-dd)."));

        if (!await IsAssignableTenantUserAsync(request.AssignedToUserId, ct))
            return BadRequest(ApiResponse<EmployeeAssignedTaskDto>.Fail("Assigned user not found or inactive."));

        task.Title = title;
        task.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        task.DueDate = dueDate.Value;
        task.AssignedToUserId = request.AssignedToUserId;
        task.Status = request.Status;
        task.UpdatedAt = DateTime.UtcNow;

        if (request.Status == EmployeeAssignedTaskStatus.Completed && !task.CompletedAtUtc.HasValue)
            task.CompletedAtUtc = DateTime.UtcNow;
        if (request.Status == EmployeeAssignedTaskStatus.Pending)
            task.CompletedAtUtc = null;

        await _db.SaveChangesAsync(ct);
        return ApiResponse<EmployeeAssignedTaskDto>.Ok(ToDto(task));
    }

    [HttpPut("{id:guid}/progress")]
    public async Task<ActionResult<ApiResponse<EmployeeAssignedTaskDto>>> UpdateTaskProgress(
        [FromRoute] Guid id,
        [FromBody] UpdateTaskProgressRequestDto request,
        CancellationToken ct = default)
    {
        var check = await EnsureCanViewAssignedTasksAsync(ct);
        if (check != null) return check;

        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
            return Unauthorized(ApiResponse<EmployeeAssignedTaskDto>.Fail("User not identified."));

        var task = await _db.EmployeeAssignedTasks
            .Include(t => t.AssignedToUser)
            .Include(t => t.AssignedByUser)
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == TenantId, ct);
        if (task == null)
            return NotFound(ApiResponse<EmployeeAssignedTaskDto>.Fail("Task not found."));

        if (!IsTenantAdmin() && task.AssignedToUserId != currentUserId.Value)
            return Forbid();

        if (task.Status != EmployeeAssignedTaskStatus.Pending)
            return BadRequest(ApiResponse<EmployeeAssignedTaskDto>.Fail("Only pending tasks can be updated."));

        task.AssigneeNotes = string.IsNullOrWhiteSpace(request.AssigneeNotes)
            ? null
            : request.AssigneeNotes.Trim();
        task.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return ApiResponse<EmployeeAssignedTaskDto>.Ok(ToDto(task));
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<ActionResult<ApiResponse<EmployeeAssignedTaskDto>>> CompleteTask(
        [FromRoute] Guid id,
        [FromBody] CompleteEmployeeAssignedTaskRequestDto? request,
        CancellationToken ct = default)
    {
        var check = await EnsureCanViewAssignedTasksAsync(ct);
        if (check != null) return check;

        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
            return Unauthorized(ApiResponse<EmployeeAssignedTaskDto>.Fail("User not identified."));

        var task = await _db.EmployeeAssignedTasks
            .Include(t => t.AssignedToUser)
            .Include(t => t.AssignedByUser)
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == TenantId, ct);
        if (task == null)
            return NotFound(ApiResponse<EmployeeAssignedTaskDto>.Fail("Task not found."));

        if (!IsTenantAdmin() && task.AssignedToUserId != currentUserId.Value)
            return Forbid();

        if (task.Status == EmployeeAssignedTaskStatus.Cancelled)
            return BadRequest(ApiResponse<EmployeeAssignedTaskDto>.Fail("Cancelled tasks cannot be completed."));
        if (task.Status == EmployeeAssignedTaskStatus.Completed)
            return BadRequest(ApiResponse<EmployeeAssignedTaskDto>.Fail("Task is already completed."));

        if (request != null && !string.IsNullOrWhiteSpace(request.AssigneeNotes))
            task.AssigneeNotes = request.AssigneeNotes.Trim();

        task.Status = EmployeeAssignedTaskStatus.Completed;
        task.CompletedAtUtc = DateTime.UtcNow;
        task.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return ApiResponse<EmployeeAssignedTaskDto>.Ok(ToDto(task));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<ApiResponse<EmployeeAssignedTaskDto>>> CancelTask(
        [FromRoute] Guid id,
        CancellationToken ct = default)
    {
        var check = await EnsureEmployeeModuleAsync(ct);
        if (check != null) return check;

        var adminDenied = DenyUnlessTenantAdmin();
        if (adminDenied != null) return adminDenied;

        var task = await _db.EmployeeAssignedTasks
            .Include(t => t.AssignedToUser)
            .Include(t => t.AssignedByUser)
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == TenantId, ct);
        if (task == null)
            return NotFound(ApiResponse<EmployeeAssignedTaskDto>.Fail("Task not found."));

        if (task.Status == EmployeeAssignedTaskStatus.Completed)
            return BadRequest(ApiResponse<EmployeeAssignedTaskDto>.Fail("Completed tasks cannot be cancelled."));

        task.Status = EmployeeAssignedTaskStatus.Cancelled;
        task.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return ApiResponse<EmployeeAssignedTaskDto>.Ok(ToDto(task));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteTask([FromRoute] Guid id, CancellationToken ct = default)
    {
        var check = await EnsureEmployeeModuleAsync(ct);
        if (check != null) return check;

        var adminDenied = DenyUnlessTenantAdmin();
        if (adminDenied != null) return adminDenied;

        var task = await _db.EmployeeAssignedTasks.FirstOrDefaultAsync(t => t.Id == id && t.TenantId == TenantId, ct);
        if (task == null)
            return NotFound(ApiResponse<object>.Fail("Task not found."));

        task.IsDeleted = true;
        task.DeletedAtUtc = DateTime.UtcNow;
        task.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return ApiResponse<object>.Ok(new { deleted = true });
    }
}
