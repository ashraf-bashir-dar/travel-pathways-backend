namespace TravelPathways.Api.Common;

/// <summary>Role-based rules for tenant module assignment and effective access.</summary>
public static class UserModulePolicy
{
  private static readonly AppModuleKey[] AdminOnlyModules =
  [
    AppModuleKey.TimeSheet,
    AppModuleKey.EmployeeManagement,
    AppModuleKey.EmployeeMonitoring
  ];

  private static readonly AppModuleKey[] AssigneeOnlyModules = [AppModuleKey.Tasks];

  public static bool IsAdminRole(UserRole role) =>
    role is UserRole.Admin or UserRole.SuperAdmin;

  public static List<AppModuleKey> SanitizeAllowedModules(UserRole role, IEnumerable<AppModuleKey>? modules)
  {
    var list = modules?.Distinct().ToList() ?? [];
    if (IsAdminRole(role))
      return list.Where(m => !AssigneeOnlyModules.Contains(m)).ToList();
    return list.Where(m => !AdminOnlyModules.Contains(m)).ToList();
  }

  public static List<AppModuleKey> ApplyRoleFilter(UserRole role, IReadOnlyList<AppModuleKey> effective) =>
    IsAdminRole(role)
      ? effective.Where(m => !AssigneeOnlyModules.Contains(m)).ToList()
      : effective.Where(m => !AdminOnlyModules.Contains(m)).ToList();
}
