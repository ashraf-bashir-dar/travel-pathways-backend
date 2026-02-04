using Microsoft.AspNetCore.Mvc;
using TravelPathways.Api.MultiTenancy;

namespace TravelPathways.Api.Controllers;

public abstract class TenantControllerBase : ControllerBase
{
  private readonly TenantContext _tenant;

  protected TenantControllerBase(TenantContext tenant)
  {
    _tenant = tenant;
  }

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
}

