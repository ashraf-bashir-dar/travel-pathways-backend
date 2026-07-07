namespace TravelPathways.Api.Common;

/// <summary>Modules where Own vs All data scope applies (list/read filtering).</summary>
public static class ModuleScopePolicy
{
    private static readonly HashSet<AppModuleKey> Scopable = new()
    {
        AppModuleKey.Leads,
        AppModuleKey.Packages,
        AppModuleKey.Sales,
        AppModuleKey.Tasks,
        AppModuleKey.Accounts,
        AppModuleKey.Ledger,
        AppModuleKey.Reservations,
        AppModuleKey.CallLogs
    };

    public static bool SupportsDataScope(AppModuleKey module) => Scopable.Contains(module);

    public static ModuleDataScope DefaultScopeForRole(UserRole role, AppModuleKey module)
    {
        if (!SupportsDataScope(module))
            return ModuleDataScope.All;

        return UserModulePolicy.IsAdminRole(role) ? ModuleDataScope.All : ModuleDataScope.Own;
    }
}
