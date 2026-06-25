namespace TravelPathways.Api.Common;

using TravelPathways.Api.Data.Entities;

/// <summary>Resolve and update employee lifecycle status from stored fields.</summary>
public static class EmployeeLifecycleHelper
{
    public static EmployeeLifecycleStatus Resolve(AppUser user)
    {
        if (user.LifecycleStatus.HasValue)
            return user.LifecycleStatus.Value;

        if (user.LeaveDate.HasValue && !user.IsActive)
            return EmployeeLifecycleStatus.Exited;
        if (!user.IsActive)
            return EmployeeLifecycleStatus.Exited;
        if (!user.JoinDate.HasValue)
            return EmployeeLifecycleStatus.Onboarding;
        return EmployeeLifecycleStatus.Active;
    }

    public static void ApplyOnCreate(AppUser user)
    {
        user.LifecycleStatus = user.JoinDate.HasValue
            ? EmployeeLifecycleStatus.Active
            : EmployeeLifecycleStatus.Onboarding;
    }

    public static void CompleteOnboarding(AppUser user, DateTime? joinDate = null)
    {
        user.JoinDate = joinDate.HasValue ? DateTimeUtcHelper.ToUtcDate(joinDate.Value) : DateTimeUtcHelper.UtcToday();
        user.LifecycleStatus = EmployeeLifecycleStatus.Active;
        user.IsActive = true;
        user.LeaveDate = null;
    }

    public static void InitiateExit(AppUser user)
    {
        user.LifecycleStatus = EmployeeLifecycleStatus.OnExit;
    }

    public static void CompleteExit(AppUser user, DateTime? leaveDate = null)
    {
        user.LeaveDate = leaveDate.HasValue ? DateTimeUtcHelper.ToUtcDate(leaveDate.Value) : DateTimeUtcHelper.UtcToday();
        user.LifecycleStatus = EmployeeLifecycleStatus.Exited;
        user.IsActive = false;
    }
}
