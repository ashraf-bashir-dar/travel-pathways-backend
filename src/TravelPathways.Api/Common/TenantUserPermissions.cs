using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelPathways.Api.Data;
using TravelPathways.Api.Data.Entities;

namespace TravelPathways.Api.Common;

public static class TenantUserPermissions
{
    public static async Task<AppUser?> LoadCurrentUserAsync(
        AppDbContext db,
        Guid tenantId,
        ClaimsPrincipal principal,
        CancellationToken ct)
    {
        var claim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(claim, out var userId))
            return null;

        return await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, ct);
    }

    public static ActionResult? DenyUnless(AppUser? user, AppModuleKey module, ModuleAction action) =>
        user is not null && ModulePermissionResolver.Can(user, module, action)
            ? null
            : StatusCode(403, ApiResponse<object>.Fail(
                $"You do not have {action.ToString().ToLowerInvariant()} access for {module}."));

    private static ObjectResult StatusCode(int code, ApiResponse<object> body) =>
        new(body) { StatusCode = code };
}
