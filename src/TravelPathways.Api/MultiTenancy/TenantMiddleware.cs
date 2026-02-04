using System.Security.Claims;
using TravelPathways.Api.Common;

namespace TravelPathways.Api.MultiTenancy;

public sealed class TenantMiddleware : IMiddleware
{
    public const string TenantHeader = "X-Tenant-Id";

    private readonly TenantContext _tenant;

    public TenantMiddleware(TenantContext tenant)
    {
        _tenant = tenant;
    }

    public Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var user = context.User;
        var role = user.FindFirstValue(ClaimTypes.Role) ?? user.FindFirstValue("role");
        var isSuperAdmin = string.Equals(role, UserRole.SuperAdmin.ToString(), StringComparison.OrdinalIgnoreCase);

        _tenant.IsSuperAdmin = isSuperAdmin;
        _tenant.TenantId = null;

        if (!isSuperAdmin)
        {
            var tenantIdClaim = user.FindFirstValue("tenantId");
            if (Guid.TryParse(tenantIdClaim, out var tid))
            {
                _tenant.TenantId = tid;
            }
        }
        else
        {
            // Optional: allow super admin to scope queries via header
            if (context.Request.Headers.TryGetValue(TenantHeader, out var header) &&
                Guid.TryParse(header.ToString(), out var tid))
            {
                _tenant.TenantId = tid;
            }
        }

        return next(context);
    }
}

