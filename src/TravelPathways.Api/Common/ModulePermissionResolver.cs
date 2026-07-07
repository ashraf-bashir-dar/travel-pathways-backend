using TravelPathways.Api.Data.Entities;

namespace TravelPathways.Api.Common;

/// <summary>Resolves per-module actions, tab access, and data scope.</summary>
public static class ModulePermissionResolver
{
    public static bool HasModuleTab(AppUser user, AppModuleKey module)
    {
        var stored = FindGrant(user.ModulePermissions, module);
        if (stored is not null)
            return stored.View;

        var allowed = user.AllowedModules ?? [];
        if (allowed.Count == 0)
            return true;

        return allowed.Contains(module);
    }

    public static ModulePermissionGrant GetEffectiveGrant(AppUser user, AppModuleKey module)
    {
        var stored = FindGrant(user.ModulePermissions, module);
        if (stored is not null)
            return CloneGrant(stored);

        // User has explicit grants saved — missing module means no access, not role defaults.
        if (user.ModulePermissions is { Count: > 0 })
        {
            return new ModulePermissionGrant
            {
                Module = module,
                View = false,
                Create = false,
                Edit = false,
                Delete = false,
                DataScope = ModuleScopePolicy.SupportsDataScope(module)
                    ? ModuleScopePolicy.DefaultScopeForRole(user.Role, module)
                    : ModuleDataScope.All
            };
        }

        return BuildDefaultGrant(user.Role, module);
    }

    public static bool Can(AppUser user, AppModuleKey module, ModuleAction action)
    {
        var grant = GetEffectiveGrant(user, module);

        // Legacy users: empty stored grants + explicit allowedModules list.
        var stored = user.ModulePermissions ?? [];
        if (stored.Count == 0)
        {
            var allowed = user.AllowedModules ?? [];
            if (allowed.Count > 0 && !allowed.Contains(module))
                return false;
        }

        return action switch
        {
            ModuleAction.View => grant.View,
            ModuleAction.Create => grant.View && grant.Create,
            ModuleAction.Edit => grant.View && grant.Edit,
            ModuleAction.Delete => grant.View && grant.Delete,
            _ => false
        };
    }

    public static ModuleDataScope GetDataScope(AppUser user, AppModuleKey module)
    {
        if (!ModuleScopePolicy.SupportsDataScope(module))
            return ModuleDataScope.All;

        return GetEffectiveGrant(user, module).DataScope;
    }

    public static bool HasDataScopeAll(AppUser user, AppModuleKey module) =>
        GetDataScope(user, module) == ModuleDataScope.All;

    public static bool CanSeeAllLeads(AppUser user) => HasDataScopeAll(user, AppModuleKey.Leads);

    public static bool CanSeeAllPackages(AppUser user) => HasDataScopeAll(user, AppModuleKey.Packages);

    public static bool CanSeeAllSales(AppUser user) => HasDataScopeAll(user, AppModuleKey.Sales);

    public static bool CanSeeAllPayments(AppUser user) =>
        HasDataScopeAll(user, AppModuleKey.Accounts) || HasDataScopeAll(user, AppModuleKey.Ledger);

    public static bool CanSeeAllTasks(AppUser user) => HasDataScopeAll(user, AppModuleKey.Tasks);

    public static bool CanPaymentAction(AppUser user, ModuleAction action) =>
        Can(user, AppModuleKey.Accounts, action) || Can(user, AppModuleKey.Ledger, action);

    public static ModulePermissionGrant? FindGrant(IReadOnlyList<ModulePermissionGrant>? grants, AppModuleKey module) =>
        grants?.FirstOrDefault(g => g.Module == module);

    public static void ApplyCreateDefaults(AppUser user, IReadOnlyList<AppModuleKey>? tenantEnabledModules)
    {
        user.AllowedModules = [];
        user.CanViewCostBifurcation = false;
        user.CanPriceOverride = false;
        user.ActivityTrackingEnabled = true;
        user.ModulePermissions = BuildDefaultGrants(user.Role, tenantEnabledModules);
    }

    public static List<ModulePermissionGrant> BuildDefaultGrants(
        UserRole role,
        IReadOnlyList<AppModuleKey>? tenantEnabledModules)
    {
        var modules = ResolveModulesForDefaults(tenantEnabledModules);
        return modules.Select(m => BuildDefaultGrant(role, m)).ToList();
    }

    public static ModulePermissionGrant BuildDefaultGrant(UserRole role, AppModuleKey module) =>
        new()
        {
            Module = module,
            View = true,
            Create = true,
            Edit = true,
            Delete = true,
            DataScope = ModuleScopePolicy.DefaultScopeForRole(role, module)
        };

    public static List<ModulePermissionGrant> SanitizeGrants(
        UserRole role,
        IReadOnlyList<ModulePermissionGrant>? incoming,
        IReadOnlyList<AppModuleKey> tenantEnabledModules)
    {
        var pool = ResolveModulesForDefaults(tenantEnabledModules);
        var assignable = UserModulePolicy.SanitizeAllowedModules(role, pool);
        var incomingByModule = (incoming ?? [])
            .GroupBy(g => g.Module)
            .ToDictionary(g => g.Key, g => g.Last());

        var result = new List<ModulePermissionGrant>();
        foreach (var module in assignable)
        {
            var grant = incomingByModule.TryGetValue(module, out var raw)
                ? NormalizeGrant(raw, role, module)
                : BuildDefaultGrant(role, module);
            result.Add(grant);
        }

        return result;
    }

    public static List<AppModuleKey> DeriveAllowedModulesFromGrants(IReadOnlyList<ModulePermissionGrant> grants)
    {
        var withView = grants.Where(g => g.View).Select(g => g.Module).Distinct().ToList();
        return withView;
    }

    public static void ApplyGrantsToUser(
        AppUser user,
        IReadOnlyList<ModulePermissionGrant> grants,
        IReadOnlyList<AppModuleKey> tenantEnabledModules)
    {
        var sanitized = SanitizeGrants(user.Role, grants, tenantEnabledModules);
        user.ModulePermissions = sanitized;
        var derived = DeriveAllowedModulesFromGrants(sanitized);
        var pool = ResolveModulesForDefaults(tenantEnabledModules);
        var assignable = UserModulePolicy.SanitizeAllowedModules(user.Role, pool);
        user.AllowedModules = derived.Count >= assignable.Count ? [] : derived;
    }

    private static ModulePermissionGrant NormalizeGrant(ModulePermissionGrant raw, UserRole role, AppModuleKey module)
    {
        var grant = new ModulePermissionGrant
        {
            Module = module,
            View = raw.View,
            Create = raw.Create,
            Edit = raw.Edit,
            Delete = raw.Delete,
            DataScope = ModuleScopePolicy.SupportsDataScope(module)
                ? raw.DataScope
                : ModuleDataScope.All
        };

        if (!grant.View)
        {
            grant.Create = false;
            grant.Edit = false;
            grant.Delete = false;
        }

        if (!ModuleScopePolicy.SupportsDataScope(module))
            grant.DataScope = ModuleDataScope.All;
        else if (!UserModulePolicy.IsAdminRole(role) && grant.DataScope == ModuleDataScope.All && !raw.View)
            grant.DataScope = ModuleScopePolicy.DefaultScopeForRole(role, module);

        return grant;
    }

    private static ModulePermissionGrant CloneGrant(ModulePermissionGrant g) =>
        new()
        {
            Module = g.Module,
            View = g.View,
            Create = g.Create,
            Edit = g.Edit,
            Delete = g.Delete,
            DataScope = g.DataScope
        };

    private static List<AppModuleKey> ResolveModulesForDefaults(IReadOnlyList<AppModuleKey>? tenantEnabledModules)
    {
        if (tenantEnabledModules is { Count: > 0 })
            return tenantEnabledModules.Distinct().ToList();

        return Enum.GetValues<AppModuleKey>()
            .Where(m => m is not (AppModuleKey.EmployeeMonitoring or AppModuleKey.VendorManagement))
            .ToList();
    }

    /// <summary>Merge stored grants with defaults for modules missing from storage (legacy users).</summary>
    public static List<ModulePermissionGrant> ResolveEffectiveGrants(
        AppUser user,
        IReadOnlyList<AppModuleKey> tenantEnabledModules)
    {
        var pool = ResolveModulesForDefaults(tenantEnabledModules);
        var assignable = UserModulePolicy.SanitizeAllowedModules(user.Role, pool);
        return assignable.Select(m => GetEffectiveGrant(user, m)).ToList();
    }
}
