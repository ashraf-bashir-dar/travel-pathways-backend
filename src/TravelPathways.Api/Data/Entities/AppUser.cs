using TravelPathways.Api.Common;

namespace TravelPathways.Api.Data.Entities;

public sealed class AppUser : EntityBase
{
    // SuperAdmin users have TenantId = null
    public Guid? TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Agent;
    public bool IsActive { get; set; } = true;

    /// <summary>Modules this user can access. Empty = all enabled for tenant.</summary>
    public List<AppModuleKey> AllowedModules { get; set; } = [];

    // Stored as PBKDF2 hash string (see PasswordHasher)
    public string PasswordHash { get; set; } = string.Empty;
}

