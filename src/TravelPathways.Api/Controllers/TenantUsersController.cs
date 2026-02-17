using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelPathways.Api.Common;
using TravelPathways.Api.Data;
using TravelPathways.Api.Data.Entities;
using TravelPathways.Api.MultiTenancy;

namespace TravelPathways.Api.Controllers;

/// <summary>
/// Tenant-scoped user list. Returns users that the current user is allowed to see:
/// - Super Admin (with X-Tenant-Id): all users for that tenant.
/// - Tenant user: only users that share at least one allowed module with the current user.
/// </summary>
[ApiController]
[Authorize]
[Route("api/tenant/users")]
public sealed class TenantUsersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TenantContext _tenant;

    public TenantUsersController(AppDbContext db, TenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public sealed class TenantUserDto
    {
        public required string Id { get; init; }
        public required string Email { get; init; }
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
        public required string TenantId { get; init; }
        public required UserRole Role { get; init; }
        public UserDepartment? Department { get; init; }
        public List<AppModuleKey>? AllowedModules { get; init; }
        public required bool IsActive { get; init; }
        public required DateTime CreatedAt { get; init; }
    }

    /// <summary>
    /// List users for the current tenant. Results are filtered by the caller's assigned modules:
    /// only users that share at least one allowed module with the current user are returned.
    /// Super Admin (with X-Tenant-Id) sees all users for that tenant.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<TenantUserDto>>>> GetUsers(CancellationToken ct)
    {
        Guid tenantId;
        try
        {
            tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant context is required.");
        }
        catch (InvalidOperationException)
        {
            return BadRequest(ApiResponse<List<TenantUserDto>>.Fail("Tenant context is missing. Send X-Tenant-Id for Super Admin, or ensure you are logged in as a tenant user."));
        }

        var allUsers = await _db.Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId)
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync(ct);

        List<AppUser> filtered;
        if (_tenant.IsSuperAdmin)
        {
            filtered = allUsers;
        }
        else
        {
            var callerIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(callerIdClaim) || !Guid.TryParse(callerIdClaim, out var callerId))
                return Unauthorized(ApiResponse<List<TenantUserDto>>.Fail("User not found."));

            var caller = await _db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == callerId, ct);
            if (caller is null)
                return Unauthorized(ApiResponse<List<TenantUserDto>>.Fail("User not found."));

            var callerModules = caller.AllowedModules ?? new List<AppModuleKey>();
            var callerSet = callerModules.Count == 0 ? null : new HashSet<AppModuleKey>(callerModules);

            filtered = allUsers.Where(u =>
            {
                if (callerSet is null) return true; // caller has "all modules" -> see everyone
                var uModules = u.AllowedModules ?? new List<AppModuleKey>();
                if (uModules.Count == 0) return true; // user has "all modules" -> share with caller
                return uModules.Any(m => callerSet.Contains(m));
            }).ToList();
        }

        var list = filtered.Select(ToDto).ToList();
        return ApiResponse<List<TenantUserDto>>.Ok(list);
    }

    private static TenantUserDto ToDto(AppUser u) =>
        new()
        {
            Id = u.Id.ToString("D"),
            Email = u.Email,
            FirstName = u.FirstName,
            LastName = u.LastName,
            TenantId = u.TenantId?.ToString("D") ?? string.Empty,
            Role = u.Role,
            Department = u.Department,
            AllowedModules = u.AllowedModules?.ToList(),
            IsActive = u.IsActive,
            CreatedAt = u.CreatedAt
        };
}
