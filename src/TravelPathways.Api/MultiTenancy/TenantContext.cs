namespace TravelPathways.Api.MultiTenancy;

public sealed class TenantContext
{
    public Guid? TenantId { get; set; }
    public bool IsSuperAdmin { get; set; }
}

