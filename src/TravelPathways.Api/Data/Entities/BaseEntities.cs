namespace TravelPathways.Api.Data.Entities;

public abstract class EntityBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Soft delete: when true, record is excluded from default queries.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Set when IsDeleted is set to true.</summary>
    public DateTime? DeletedAtUtc { get; set; }
}

public abstract class TenantEntityBase : EntityBase
{
    public Guid TenantId { get; set; }
    public bool IsActive { get; set; } = true;
}

