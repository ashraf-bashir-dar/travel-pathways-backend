namespace TravelPathways.Api.Common;

/// <summary>
/// Effective module access: intersection of tenant enabled modules and user allowed modules.
/// Empty user list means all tenant-enabled modules (same as frontend).
/// </summary>
public static class ModuleAccess
{
    public static List<AppModuleKey> GetEffectiveModules(
        IReadOnlyList<AppModuleKey>? tenantEnabled,
        IReadOnlyList<AppModuleKey>? userAllowed)
    {
        var tenant = tenantEnabled ?? [];
        var user = userAllowed ?? [];
        if (tenant.Count == 0)
            return user.Count == 0 ? [] : user.ToList();
        if (user.Count == 0)
            return tenant.ToList();
        return tenant.Where(m => user.Contains(m)).ToList();
    }

    public static bool HasModule(IReadOnlyList<AppModuleKey> effective, AppModuleKey module) =>
        effective.Contains(module);
}
