using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using TravelPathways.Api.Common;
using TravelPathways.Api.Data;
using TravelPathways.Api.Data.Entities;
using TravelPathways.Api.MultiTenancy;

namespace TravelPathways.Api.Controllers;

[ServiceFilter(typeof(RequireTenantActionFilter))]
public abstract class TenantControllerBase : ControllerBase
{
  private readonly TenantContext _tenant;

  protected TenantControllerBase(TenantContext tenant)
  {
    _tenant = tenant;
  }

  /// <summary>True when the current request has a tenant context (X-Tenant-Id or tenant user).</summary>
  protected bool HasTenantId => _tenant.TenantId.HasValue;

  protected Guid TenantId
  {
    get
    {
      if (_tenant.TenantId is { } tid) return tid;
      throw new InvalidOperationException(
          _tenant.IsSuperAdmin
              ? "Tenant context is missing. Super Admin must send X-Tenant-Id header to access tenant-scoped resources."
              : "Tenant context is missing. Your session may have no tenant; try logging in again.");
    }
  }

  /// <summary>True for tenant Admin or Super Admin.</summary>
  protected bool IsTenantAdmin() => ControllerAuthorization.IsTenantAdmin(User);

  /// <summary>Returns Forbid() when the current user is not a tenant admin.</summary>
  protected ActionResult? DenyUnlessTenantAdmin() => IsTenantAdmin() ? null : Forbid();

  protected async Task<AppUser?> LoadCurrentAppUserAsync(AppDbContext db, CancellationToken ct) =>
      await TenantUserPermissions.LoadCurrentUserAsync(db, TenantId, User, ct);

  protected async Task<ActionResult?> DenyUnlessModuleActionAsync(
      AppDbContext db,
      AppModuleKey module,
      ModuleAction action,
      CancellationToken ct)
  {
    var user = await LoadCurrentAppUserAsync(db, ct);
    return TenantUserPermissions.DenyUnless(user, module, action);
  }
}

