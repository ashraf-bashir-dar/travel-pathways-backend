using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TravelPathways.Api.Common;
using TravelPathways.Api.Controllers;

namespace TravelPathways.Api.MultiTenancy;

/// <summary>Returns 400 when a tenant-scoped controller is invoked without tenant context.</summary>
public sealed class RequireTenantActionFilter : IActionFilter
{
    private readonly TenantContext _tenant;

    public RequireTenantActionFilter(TenantContext tenant) => _tenant = tenant;

    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.Controller is not TenantControllerBase)
            return;

        if (_tenant.TenantId.HasValue)
            return;

        var message = _tenant.IsSuperAdmin
            ? "Tenant context is missing. Super Admin must send X-Tenant-Id header to access tenant-scoped resources."
            : "Tenant context is missing. Your session may have no tenant; try logging in again.";

        context.Result = new BadRequestObjectResult(ApiResponse<object>.Fail(message));
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}
