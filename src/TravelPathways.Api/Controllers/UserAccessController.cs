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

/// <summary>Manage per-user module access, feature flags, and data scope.</summary>
[ApiController]
[Authorize]
[Route("api/tenant/user-access")]
public sealed class UserAccessController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TenantContext _tenant;
    private readonly IPasswordEncryption _passwordEncryption;

    public UserAccessController(AppDbContext db, TenantContext tenant, IPasswordEncryption passwordEncryption)
    {
        _db = db;
        _tenant = tenant;
        _passwordEncryption = passwordEncryption;
    }

    public sealed class UserAccessSummaryDto
    {
        public required string Id { get; init; }
        public required string Email { get; init; }
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
        public required UserRole Role { get; init; }
        public required bool IsActive { get; init; }
        public int ModuleCount { get; init; }
        public ModuleDataScope LeadsDataScope { get; init; }
    }

    public sealed class UserAccessDetailDto
    {
        public required string Id { get; init; }
        public required string Email { get; init; }
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
        public required UserRole Role { get; init; }
        public List<AppModuleKey> AllowedModules { get; init; } = [];
        public bool CanViewCostBifurcation { get; init; }
        public bool CanPriceOverride { get; init; }
        public bool ActivityTrackingEnabled { get; init; } = true;
        public ModuleDataScope LeadsDataScope { get; init; }
        public List<ModulePermissionGrantDto> ModulePermissions { get; init; } = [];
        public List<AppModuleKey> TenantEnabledModules { get; init; } = [];
    }

    public sealed class UpdateUserAccessRequestDto
    {
        public List<AppModuleKey>? AllowedModules { get; set; }
        public bool CanViewCostBifurcation { get; set; }
        public bool CanPriceOverride { get; set; }
        public bool ActivityTrackingEnabled { get; set; } = true;
        /// <summary>Legacy; prefer <see cref="ModulePermissions"/>.</summary>
        public ModuleDataScope? LeadsDataScope { get; set; }
        public List<ModulePermissionGrantDto>? ModulePermissions { get; set; }
        /// <summary>Optional. Min 6 characters. Leave blank to keep existing password.</summary>
        public string? Password { get; set; }
    }

    public sealed class PasswordResponseDto
    {
        public required string Password { get; init; }
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<UserAccessSummaryDto>>>> GetSummaries(CancellationToken ct)
    {
        var denied = await EnsureCanManageUserAccessAsync(ct);
        if (denied is not null) return denied;

        var tenantId = _tenant.TenantId!.Value;
        var users = await _db.Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId)
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .ToListAsync(ct);

        var list = users.Select(u => new UserAccessSummaryDto
        {
            Id = u.Id.ToString("D"),
            Email = u.Email,
            FirstName = u.FirstName,
            LastName = u.LastName,
            Role = u.Role,
            IsActive = u.IsActive,
            ModuleCount = u.AllowedModules?.Count ?? 0,
            LeadsDataScope = ModulePermissionResolver.GetDataScope(u, AppModuleKey.Leads)
        }).ToList();

        return ApiResponse<List<UserAccessSummaryDto>>.Ok(list);
    }

    [HttpGet("{userId:guid}")]
    public async Task<ActionResult<ApiResponse<UserAccessDetailDto>>> GetDetail([FromRoute] Guid userId, CancellationToken ct)
    {
        var denied = await EnsureCanManageUserAccessAsync(ct);
        if (denied is not null) return denied;

        var tenantId = _tenant.TenantId!.Value;
        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Id == userId, ct);
        if (user is null)
            return NotFound(ApiResponse<UserAccessDetailDto>.Fail("User not found."));

        var tenantModules = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => t.EnabledModules)
            .FirstOrDefaultAsync(ct) ?? [];

        return ApiResponse<UserAccessDetailDto>.Ok(ToDetailDto(user, tenantModules));
    }

    /// <summary>Get the user's stored password (reversible). Requires User Access management rights.</summary>
    [HttpGet("{userId:guid}/password")]
    public async Task<ActionResult<ApiResponse<PasswordResponseDto>>> GetPassword([FromRoute] Guid userId, CancellationToken ct)
    {
        var denied = await EnsureCanManageUserAccessAsync(ct);
        if (denied is not null) return denied;

        var tenantId = _tenant.TenantId!.Value;
        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Id == userId, ct);
        if (user is null)
            return NotFound(ApiResponse<PasswordResponseDto>.Fail("User not found."));

        var password = _passwordEncryption.Decrypt(user.PasswordEncrypted);
        if (password is null)
            return NotFound(ApiResponse<PasswordResponseDto>.Fail(
                "Password is not available for this user. Set a new password below to store it for viewing."));

        return ApiResponse<PasswordResponseDto>.Ok(new PasswordResponseDto { Password = password });
    }

    [HttpPut("{userId:guid}")]
    public async Task<ActionResult<ApiResponse<UserAccessDetailDto>>> UpdateAccess(
        [FromRoute] Guid userId,
        [FromBody] UpdateUserAccessRequestDto request,
        CancellationToken ct)
    {
        var denied = await EnsureCanManageUserAccessAsync(ct);
        if (denied is not null) return denied;

        if (request.CanViewCostBifurcation && request.CanPriceOverride)
            return BadRequest(ApiResponse<UserAccessDetailDto>.Fail(
                "Can view cost bifurcation and Price Override cannot both be enabled for the same user."));

        var tenantId = _tenant.TenantId!.Value;
        var user = await _db.Users.FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Id == userId, ct);
        if (user is null)
            return NotFound(ApiResponse<UserAccessDetailDto>.Fail("User not found."));

        var tenantModules = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => t.EnabledModules)
            .FirstOrDefaultAsync(ct) ?? [];

        user.CanViewCostBifurcation = request.CanViewCostBifurcation;
        user.CanPriceOverride = request.CanPriceOverride;
        user.ActivityTrackingEnabled = request.ActivityTrackingEnabled;

        if (request.ModulePermissions is { Count: > 0 })
        {
            var grants = request.ModulePermissions.Select(d => new ModulePermissionGrant
            {
                Module = d.Module,
                View = d.View,
                Create = d.Create,
                Edit = d.Edit,
                Delete = d.Delete,
                DataScope = d.DataScope
            }).ToList();
            ModulePermissionResolver.ApplyGrantsToUser(user, grants, tenantModules);
        }
        else if (request.LeadsDataScope.HasValue)
        {
            var grants = ModulePermissionResolver.ResolveEffectiveGrants(user, tenantModules);
            var leads = grants.FirstOrDefault(g => g.Module == AppModuleKey.Leads);
            if (leads is not null)
                leads.DataScope = request.LeadsDataScope.Value;
            ModulePermissionResolver.ApplyGrantsToUser(user, grants, tenantModules);
        }
        else if (request.AllowedModules is not null)
        {
            user.AllowedModules = UserModulePolicy.SanitizeAllowedModules(
                user.Role,
                SanitizeModulesAgainstTenant(request.AllowedModules, tenantModules));
        }

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            if (request.Password.Trim().Length < 6)
                return BadRequest(ApiResponse<UserAccessDetailDto>.Fail("Password must be at least 6 characters."));

            user.PasswordHash = PasswordHasher.Hash(request.Password);
            user.PasswordEncrypted = _passwordEncryption.Encrypt(request.Password);
        }

        await _db.SaveChangesAsync(ct);

        return ApiResponse<UserAccessDetailDto>.Ok(ToDetailDto(user, tenantModules));
    }

    private static List<AppModuleKey> SanitizeModulesAgainstTenant(
        List<AppModuleKey>? requested,
        IReadOnlyList<AppModuleKey> tenantModules)
    {
        var list = requested?.Distinct().ToList() ?? [];
        if (tenantModules.Count == 0)
            return list;

        var allowed = new HashSet<AppModuleKey>(tenantModules);
        return list.Where(allowed.Contains).ToList();
    }

    private async Task<ActionResult?> EnsureCanManageUserAccessAsync(CancellationToken ct)
    {
        if (!_tenant.TenantId.HasValue)
            return BadRequest(ApiResponse<object>.Fail("Tenant context is missing."));

        if (_tenant.IsSuperAdmin)
            return null;

        var callerId = GetCurrentUserId();
        if (!callerId.HasValue)
            return Unauthorized(ApiResponse<object>.Fail("User not identified."));

        var caller = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == callerId.Value && u.TenantId == _tenant.TenantId, ct);
        if (caller is null)
            return Unauthorized(ApiResponse<object>.Fail("User not found."));

        var tenantModules = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == _tenant.TenantId)
            .Select(t => t.EnabledModules)
            .FirstOrDefaultAsync(ct) ?? [];

        var effective = ModuleAccess.GetEffectiveModules(tenantModules, caller.AllowedModules);
        if (ModuleAccess.HasModule(effective, AppModuleKey.UserAccess))
            return null;

        if (UserModulePolicy.IsAdminRole(caller.Role))
            return null;

        return StatusCode(403, ApiResponse<object>.Fail("User Access module is not available for your account."));
    }

    private Guid? GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    private static UserAccessDetailDto ToDetailDto(AppUser user, IReadOnlyList<AppModuleKey> tenantModules) =>
        new()
        {
            Id = user.Id.ToString("D"),
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            AllowedModules = user.AllowedModules?.ToList() ?? [],
            CanViewCostBifurcation = user.CanViewCostBifurcation,
            CanPriceOverride = user.CanPriceOverride,
            ActivityTrackingEnabled = user.ActivityTrackingEnabled,
            LeadsDataScope = ModulePermissionResolver.GetDataScope(user, AppModuleKey.Leads),
            ModulePermissions = ModulePermissionResolver.ResolveEffectiveGrants(user, tenantModules)
                .Select(ToGrantDto).ToList(),
            TenantEnabledModules = tenantModules.ToList()
        };

    private static ModulePermissionGrantDto ToGrantDto(ModulePermissionGrant g) =>
        new()
        {
            Module = g.Module,
            View = g.View,
            Create = g.Create,
            Edit = g.Edit,
            Delete = g.Delete,
            DataScope = g.DataScope
        };
}
