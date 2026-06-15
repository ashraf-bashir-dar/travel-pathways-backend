using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TravelPathways.Api.Data;

namespace TravelPathways.Api.Common;

/// <summary>Resolves effective module access for the current user within a tenant.</summary>
public static class TenantModuleResolver
{
    public static async Task<IReadOnlyList<AppModuleKey>> GetEffectiveModulesAsync(
        AppDbContext db,
        ClaimsPrincipal user,
        Guid tenantId,
        CancellationToken ct)
    {
        var tenantModules = await db.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => t.EnabledModules)
            .FirstOrDefaultAsync(ct) ?? [];

        var role = user.FindFirstValue(ClaimTypes.Role) ?? user.FindFirstValue("role");
        if (string.Equals(role, UserRole.SuperAdmin.ToString(), StringComparison.OrdinalIgnoreCase))
            return ModuleAccess.GetEffectiveModules(tenantModules, []);

        var userIdClaim = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdClaim, out var userId))
            return [];

        var userModules = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId && u.TenantId == tenantId)
            .Select(u => u.AllowedModules)
            .FirstOrDefaultAsync(ct) ?? [];

        var effective = ModuleAccess.GetEffectiveModules(tenantModules, userModules);
        if (Enum.TryParse<UserRole>(role, true, out var userRole))
            return UserModulePolicy.ApplyRoleFilter(userRole, effective);
        return effective;
    }

    public static async Task<bool> HasModuleAsync(
        AppDbContext db,
        ClaimsPrincipal user,
        Guid tenantId,
        AppModuleKey module,
        CancellationToken ct)
    {
        var effective = await GetEffectiveModulesAsync(db, user, tenantId, ct);
        return ModuleAccess.HasModule(effective, module);
    }
}
