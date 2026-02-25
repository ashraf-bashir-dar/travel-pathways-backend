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
    /// <summary>Department/user type (Sales, HR, Accounts) for display and default module suggestions.</summary>
    public UserDepartment? Department { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Modules this user can access. Empty = all enabled for tenant.</summary>
    public List<AppModuleKey> AllowedModules { get; set; } = [];

    /// <summary>If true, user can see the Cost Bifurcation section on the package form. Set by Tenant Admin when editing users.</summary>
    public bool CanViewCostBifurcation { get; set; }

    // Stored as PBKDF2 hash string (see PasswordHasher)
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Encrypted password for admin view. Null if never set or encryption unavailable.</summary>
    public string? PasswordEncrypted { get; set; }
}

