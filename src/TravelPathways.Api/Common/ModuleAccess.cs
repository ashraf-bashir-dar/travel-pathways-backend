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
        var tenant = ExpandLegacyTransportModules(tenantEnabled ?? []);
        var user = ExpandLegacyTransportModules(userAllowed ?? []);
        if (tenant.Count == 0)
            return user.Count == 0 ? [] : user.ToList();
        if (user.Count == 0)
            return tenant.ToList();
        return tenant.Where(m => user.Contains(m)).ToList();
    }

    /// <summary>Before TransportMaster existed, Transport covered both master data and tour assignments.</summary>
    public static List<AppModuleKey> ExpandLegacyTransportModules(IReadOnlyList<AppModuleKey> modules)
    {
        var list = modules.Distinct().ToList();
        if (list.Contains(AppModuleKey.Transport) && !list.Contains(AppModuleKey.TransportMaster))
            list.Add(AppModuleKey.TransportMaster);
        return list;
    }

    public static bool HasModule(IReadOnlyList<AppModuleKey> effective, AppModuleKey module) =>
        effective.Contains(module);
}
