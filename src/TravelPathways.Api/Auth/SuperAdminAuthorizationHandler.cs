using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using TravelPathways.Api.Common;

namespace TravelPathways.Api.Auth;

/// <summary>
/// Requirement for Super Admin access. Fails when SuperAdmin:Enabled is false in config.
/// </summary>
public sealed class SuperAdminRequirement : IAuthorizationRequirement
{
}

/// <summary>
/// Checks that Super Admin is enabled in config and the user has the SuperAdmin role.
/// </summary>
public sealed class SuperAdminAuthorizationHandler : AuthorizationHandler<SuperAdminRequirement>
{
    private readonly IConfiguration _config;

    public SuperAdminAuthorizationHandler(IConfiguration config)
    {
        _config = config;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        SuperAdminRequirement requirement)
    {
        var enabled = _config.GetValue<bool>("SuperAdmin:Enabled", true);
        if (!enabled)
        {
            return Task.CompletedTask; // do not call Succeed -> requirement fails
        }

        var role = context.User.FindFirstValue(ClaimTypes.Role) ?? context.User.FindFirstValue("role");
        if (string.Equals(role, UserRole.SuperAdmin.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
