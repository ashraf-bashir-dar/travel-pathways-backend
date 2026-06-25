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
[Authorize(Policy = "TenantAdminOnly")]
[Route("api/hr")]
public sealed class HrController : TenantControllerBase
{
    private readonly AppDbContext _db;
    private readonly TenantContext _tenant;

    public HrController(AppDbContext db, TenantContext tenant) : base(tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private async Task<ActionResult?> EnsureHrModuleAsync(CancellationToken ct)
    {
        if (_tenant.IsSuperAdmin) return null;
        if (!HasTenantId) return BadRequest(ApiResponse<object>.Fail("Tenant context is missing."));
        var enabled = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == TenantId)
            .Select(t => t.EnabledModules)
            .FirstOrDefaultAsync(ct);
        if (EmployeeModuleAccess.IsManagementModuleEnabled(enabled)) return null;
        return StatusCode(403, ApiResponse<object>.Fail("HR module is not enabled for this tenant."));
    }

    public sealed class HrDashboardDto
    {
        public int TotalEmployees { get; init; }
        public int ActiveEmployees { get; init; }
        public int OnboardingCount { get; init; }
        public int OnExitCount { get; init; }
        public int ExitedCount { get; init; }
        public int PendingLeaveRequests { get; init; }
        public int PresentToday { get; init; }
        public int OnLeaveToday { get; init; }
    }

    public sealed class HrEmployeeDto
    {
        public required string Id { get; init; }
        public required string FullName { get; init; }
        public required string Email { get; init; }
        public string? Designation { get; init; }
        public UserDepartment? Department { get; init; }
        public required UserRole Role { get; init; }
        public required bool IsActive { get; init; }
        public DateTime? JoinDate { get; init; }
        public DateTime? LeaveDate { get; init; }
        public required EmployeeLifecycleStatus LifecycleStatus { get; init; }
        public string? Phone { get; init; }
    }

    public sealed class CompleteOnboardingRequest
    {
        public DateTime? JoinDate { get; set; }
    }

    public sealed class CompleteExitRequest
    {
        public DateTime? LeaveDate { get; set; }
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<ApiResponse<HrDashboardDto>>> GetDashboard(CancellationToken ct)
    {
        var check = await EnsureHrModuleAsync(ct);
        if (check != null) return check;

        var users = await _db.Users.AsNoTracking()
            .Where(u => u.TenantId == TenantId && u.Role != UserRole.SuperAdmin)
            .ToListAsync(ct);

        var today = DateTimeUtcHelper.UtcToday();
        var lifecycleCounts = users.GroupBy(u => EmployeeLifecycleHelper.Resolve(u))
            .ToDictionary(g => g.Key, g => g.Count());

        var pendingLeaves = await _db.Leaves.AsNoTracking()
            .CountAsync(l => l.TenantId == TenantId && l.Status == LeaveStatus.Pending, ct);

        var presentToday = await _db.Attendances.AsNoTracking()
            .CountAsync(a => a.TenantId == TenantId && a.AttendanceDate == today && a.TimeInUtc != null, ct);

        var onLeaveToday = await _db.Leaves.AsNoTracking()
            .CountAsync(l =>
                l.TenantId == TenantId
                && l.Status == LeaveStatus.Approved
                && l.StartDate <= today
                && l.EndDate >= today, ct);

        return ApiResponse<HrDashboardDto>.Ok(new HrDashboardDto
        {
            TotalEmployees = users.Count,
            ActiveEmployees = lifecycleCounts.GetValueOrDefault(EmployeeLifecycleStatus.Active),
            OnboardingCount = lifecycleCounts.GetValueOrDefault(EmployeeLifecycleStatus.Onboarding),
            OnExitCount = lifecycleCounts.GetValueOrDefault(EmployeeLifecycleStatus.OnExit),
            ExitedCount = lifecycleCounts.GetValueOrDefault(EmployeeLifecycleStatus.Exited),
            PendingLeaveRequests = pendingLeaves,
            PresentToday = presentToday,
            OnLeaveToday = onLeaveToday
        });
    }

    [HttpGet("employees")]
    public async Task<ActionResult<ApiResponse<List<HrEmployeeDto>>>> GetEmployees(
        [FromQuery] EmployeeLifecycleStatus? status = null,
        CancellationToken ct = default)
    {
        var check = await EnsureHrModuleAsync(ct);
        if (check != null) return check;

        var users = await _db.Users.AsNoTracking()
            .Where(u => u.TenantId == TenantId && u.Role != UserRole.SuperAdmin)
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .ToListAsync(ct);

        var list = users
            .Select(ToEmployeeDto)
            .Where(d => !status.HasValue || d.LifecycleStatus == status.Value)
            .ToList();

        return ApiResponse<List<HrEmployeeDto>>.Ok(list);
    }

    [HttpPost("onboarding/{userId:guid}/complete")]
    public async Task<ActionResult<ApiResponse<HrEmployeeDto>>> CompleteOnboarding(
        [FromRoute] Guid userId,
        [FromBody] CompleteOnboardingRequest? request,
        CancellationToken ct)
    {
        var check = await EnsureHrModuleAsync(ct);
        if (check != null) return check;

        var user = await _db.Users.FirstOrDefaultAsync(u => u.TenantId == TenantId && u.Id == userId, ct);
        if (user is null) return NotFound(ApiResponse<HrEmployeeDto>.Fail("Employee not found."));

        EmployeeLifecycleHelper.CompleteOnboarding(user, request?.JoinDate);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<HrEmployeeDto>.Ok(ToEmployeeDto(user), "Onboarding completed.");
    }

    [HttpPost("exit/{userId:guid}/initiate")]
    public async Task<ActionResult<ApiResponse<HrEmployeeDto>>> InitiateExit(
        [FromRoute] Guid userId,
        CancellationToken ct)
    {
        var check = await EnsureHrModuleAsync(ct);
        if (check != null) return check;

        var user = await _db.Users.FirstOrDefaultAsync(u => u.TenantId == TenantId && u.Id == userId, ct);
        if (user is null) return NotFound(ApiResponse<HrEmployeeDto>.Fail("Employee not found."));

        var status = EmployeeLifecycleHelper.Resolve(user);
        if (status is EmployeeLifecycleStatus.Exited or EmployeeLifecycleStatus.OnExit)
            return BadRequest(ApiResponse<HrEmployeeDto>.Fail("Employee is already in exit process or has exited."));

        EmployeeLifecycleHelper.InitiateExit(user);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<HrEmployeeDto>.Ok(ToEmployeeDto(user), "Exit process initiated.");
    }

    [HttpPost("exit/{userId:guid}/complete")]
    public async Task<ActionResult<ApiResponse<HrEmployeeDto>>> CompleteExit(
        [FromRoute] Guid userId,
        [FromBody] CompleteExitRequest? request,
        CancellationToken ct)
    {
        var check = await EnsureHrModuleAsync(ct);
        if (check != null) return check;

        var user = await _db.Users.FirstOrDefaultAsync(u => u.TenantId == TenantId && u.Id == userId, ct);
        if (user is null) return NotFound(ApiResponse<HrEmployeeDto>.Fail("Employee not found."));

        EmployeeLifecycleHelper.CompleteExit(user, request?.LeaveDate);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<HrEmployeeDto>.Ok(ToEmployeeDto(user), "Employee exit completed.");
    }

    private static HrEmployeeDto ToEmployeeDto(AppUser u) =>
        new()
        {
            Id = u.Id.ToString("D"),
            FullName = $"{u.FirstName} {u.LastName}".Trim(),
            Email = u.Email,
            Designation = u.Designation,
            Department = u.Department,
            Role = u.Role,
            IsActive = u.IsActive,
            JoinDate = u.JoinDate,
            LeaveDate = u.LeaveDate,
            LifecycleStatus = EmployeeLifecycleHelper.Resolve(u),
            Phone = u.Phone
        };
}
