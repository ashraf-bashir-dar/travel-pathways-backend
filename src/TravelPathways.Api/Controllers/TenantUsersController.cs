using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelPathways.Api.Auth;
using TravelPathways.Api.Common;
using TravelPathways.Api.Data;
using TravelPathways.Api.Data.Entities;
using TravelPathways.Api.MultiTenancy;
using TravelPathways.Api.Services;

namespace TravelPathways.Api.Controllers;

/// <summary>
/// Tenant-scoped user management. List/get visible to users with shared modules; create/update/delete require Tenant Admin.
/// </summary>
[ApiController]
[Authorize]
[Route("api/tenant/users")]
public sealed class TenantUsersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TenantContext _tenant;
    private readonly IEmailService _emailService;

    public TenantUsersController(AppDbContext db, TenantContext tenant, IEmailService emailService)
    {
        _db = db;
        _tenant = tenant;
        _emailService = emailService;
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

    public sealed class CreateTenantUserRequestDto
    {
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public UserRole Role { get; set; } = UserRole.Admin;
        public UserDepartment? Department { get; set; }
        public string Password { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public List<AppModuleKey>? AllowedModules { get; set; }
    }

    public sealed class UpdateTenantUserRequestDto
    {
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public UserRole Role { get; set; } = UserRole.Agent;
        public UserDepartment? Department { get; set; }
        public string? Password { get; set; }
        public bool IsActive { get; set; } = true;
        public List<AppModuleKey>? AllowedModules { get; set; }
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

    /// <summary>
    /// Get a single user by id. Same visibility as list (shared modules or Super Admin with X-Tenant-Id).
    /// </summary>
    [HttpGet("{userId:guid}")]
    public async Task<ActionResult<ApiResponse<TenantUserDto>>> GetUserById([FromRoute] Guid userId, CancellationToken ct)
    {
        if (!_tenant.TenantId.HasValue)
            return BadRequest(ApiResponse<TenantUserDto>.Fail("Tenant context is missing."));

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.TenantId == _tenant.TenantId && u.Id == userId, ct);
        if (user is null) return NotFound(ApiResponse<TenantUserDto>.Fail("User not found."));

        if (!_tenant.IsSuperAdmin)
        {
            var callerIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(callerIdClaim) || !Guid.TryParse(callerIdClaim, out var callerId))
                return Unauthorized();
            var caller = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == callerId, ct);
            if (caller is null) return Unauthorized();
            var callerSet = (caller.AllowedModules?.Count ?? 0) == 0 ? null : new HashSet<AppModuleKey>(caller.AllowedModules!);
            var userModules = user.AllowedModules ?? new List<AppModuleKey>();
            if (userModules.Count > 0 && callerSet != null && !userModules.Any(m => callerSet.Contains(m)))
                return NotFound(ApiResponse<TenantUserDto>.Fail("User not found."));
        }

        return ApiResponse<TenantUserDto>.Ok(ToDto(user));
    }

    /// <summary>
    /// Create a user in the current tenant. Requires Tenant Admin role.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<ActionResult<ApiResponse<TenantUserDto>>> CreateUser([FromBody] CreateTenantUserRequestDto request, CancellationToken ct)
    {
        if (!_tenant.TenantId.HasValue)
            return BadRequest(ApiResponse<TenantUserDto>.Fail("Tenant context is missing."));
        var tenantId = _tenant.TenantId.Value;

        if (request.Role == UserRole.SuperAdmin) return BadRequest(ApiResponse<TenantUserDto>.Fail("Role not allowed."));
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(ApiResponse<TenantUserDto>.Fail("Email and password are required."));

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null) return BadRequest(ApiResponse<TenantUserDto>.Fail("Tenant not found."));

        if (tenant.SeatsPurchased > 0)
        {
            var activeCount = await _db.Users.CountAsync(u => u.TenantId == tenantId && u.IsActive, ct);
            if (activeCount >= tenant.SeatsPurchased)
                return BadRequest(ApiResponse<TenantUserDto>.Fail($"Seat limit reached ({tenant.SeatsPurchased} seats). Contact support to add more users."));
        }

        if (await _db.Users.AnyAsync(u => u.Email == request.Email.Trim(), ct))
            return BadRequest(ApiResponse<TenantUserDto>.Fail("A user with this email already exists."));

        var user = new AppUser
        {
            TenantId = tenantId,
            Email = request.Email.Trim(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Role = request.Role,
            Department = request.Department,
            IsActive = request.IsActive,
            AllowedModules = request.AllowedModules?.ToList() ?? [],
            PasswordHash = PasswordHasher.Hash(request.Password)
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        if (tenant.DefaultUserId is null)
        {
            tenant.DefaultUserId = user.Id;
            await _db.SaveChangesAsync(ct);
        }

        var emailSent = await _emailService.SendWelcomeEmailAsync(user.Email, user.FirstName, tenant.Name, user.Email, request.Password, ct);
        var response = ApiResponse<TenantUserDto>.Ok(ToDto(user));
        if (!emailSent) response = ApiResponse<TenantUserDto>.Ok(ToDto(user), "User created. Could not send welcome email; share login details manually.");
        return CreatedAtAction(nameof(GetUserById), new { userId = user.Id }, response);
    }

    /// <summary>
    /// Update a user in the current tenant. Requires Tenant Admin role.
    /// </summary>
    [HttpPut("{userId:guid}")]
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<ActionResult<ApiResponse<TenantUserDto>>> UpdateUser([FromRoute] Guid userId, [FromBody] UpdateTenantUserRequestDto request, CancellationToken ct)
    {
        if (!_tenant.TenantId.HasValue) return BadRequest(ApiResponse<TenantUserDto>.Fail("Tenant context is missing."));
        if (request.Role == UserRole.SuperAdmin) return BadRequest(ApiResponse<TenantUserDto>.Fail("Role not allowed."));

        var user = await _db.Users.FirstOrDefaultAsync(u => u.TenantId == _tenant.TenantId && u.Id == userId, ct);
        if (user is null) return NotFound(ApiResponse<TenantUserDto>.Fail("User not found."));

        var newEmail = request.Email.Trim();
        if (newEmail != user.Email && await _db.Users.AnyAsync(u => u.Email == newEmail, ct))
            return BadRequest(ApiResponse<TenantUserDto>.Fail("A user with this email already exists."));

        user.Email = newEmail;
        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.Role = request.Role;
        user.Department = request.Department;
        user.IsActive = request.IsActive;
        user.AllowedModules = request.AllowedModules?.ToList() ?? user.AllowedModules ?? [];
        if (!string.IsNullOrWhiteSpace(request.Password))
            user.PasswordHash = PasswordHasher.Hash(request.Password);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<TenantUserDto>.Ok(ToDto(user));
    }

    /// <summary>
    /// Delete a user in the current tenant. Requires Tenant Admin role.
    /// </summary>
    [HttpDelete("{userId:guid}")]
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteUser([FromRoute] Guid userId, CancellationToken ct)
    {
        if (!_tenant.TenantId.HasValue) return BadRequest(ApiResponse<object>.Fail("Tenant context is missing."));
        var user = await _db.Users.FirstOrDefaultAsync(u => u.TenantId == _tenant.TenantId && u.Id == userId, ct);
        if (user is null) return NotFound(ApiResponse<object>.Fail("User not found."));
        _db.Users.Remove(user);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(new { });
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
