using System.Security.Claims;
using TravelPathways.Api.Common;

namespace TravelPathways.Api.Common;

public static class ControllerAuthorization
{
    public static bool IsTenantAdmin(ClaimsPrincipal user)
    {
        var role = user.FindFirstValue(ClaimTypes.Role) ?? user.FindFirstValue("role");
        return string.Equals(role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase)
               || string.Equals(role, UserRole.SuperAdmin.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
