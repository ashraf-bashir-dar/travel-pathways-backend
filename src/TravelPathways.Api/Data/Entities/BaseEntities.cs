namespace TravelPathways.Api.Data.Entities;

public abstract class EntityBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public abstract class TenantEntityBase : EntityBase
{
    public Guid TenantId { get; set; }
    public bool IsActive { get; set; } = true;
}

