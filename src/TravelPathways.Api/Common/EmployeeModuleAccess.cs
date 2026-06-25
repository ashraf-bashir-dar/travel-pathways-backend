namespace TravelPathways.Api.Common;

/// <summary>Shared checks for employee-related tenant modules (TimeSheet, Employee Management, HR).</summary>
public static class EmployeeModuleAccess
{
    private static readonly AppModuleKey[] EmployeeModules =
    [
        AppModuleKey.TimeSheet,
        AppModuleKey.EmployeeManagement,
        AppModuleKey.EmployeeMonitoring,
        AppModuleKey.HR
    ];

    private static readonly AppModuleKey[] ManagementModules =
    [
        AppModuleKey.EmployeeManagement,
        AppModuleKey.EmployeeMonitoring,
        AppModuleKey.HR
    ];

    public static bool IsEmployeeModuleEnabled(IReadOnlyList<AppModuleKey>? enabledModules)
    {
        if (enabledModules == null || enabledModules.Count == 0) return true;
        return enabledModules.Any(m => EmployeeModules.Contains(m));
    }

    public static bool IsManagementModuleEnabled(IReadOnlyList<AppModuleKey>? enabledModules)
    {
        if (enabledModules == null || enabledModules.Count == 0) return true;
        return enabledModules.Any(m => ManagementModules.Contains(m));
    }
}
