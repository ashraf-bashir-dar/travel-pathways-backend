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

    /// <summary>Employee/contact phone number.</summary>
    public string? Phone { get; set; }
    /// <summary>Date of birth (date only, stored as UTC midnight).</summary>
    public DateTime? DateOfBirth { get; set; }
    /// <summary>Date the employee joined the organization.</summary>
    public DateTime? JoinDate { get; set; }
    /// <summary>Date the employee left the organization (if applicable).</summary>
    public DateTime? LeaveDate { get; set; }
    /// <summary>Job title or designation (e.g. "Senior Tour Manager", "Sales Executive").</summary>
    public string? Designation { get; set; }
    /// <summary>Residential or official address.</summary>
    public string? Address { get; set; }
    /// <summary>Emergency contact person name.</summary>
    public string? EmergencyContactName { get; set; }
    /// <summary>Emergency contact phone number.</summary>
    public string? EmergencyContactPhone { get; set; }
    /// <summary>Optional profile photo URL (e.g. from file upload).</summary>
    public string? ProfilePhotoUrl { get; set; }

    public UserRole Role { get; set; } = UserRole.Agent;
    /// <summary>Department/user type (Sales, HR, Accounts) for display and default module suggestions.</summary>
    public UserDepartment? Department { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Modules this user can access. Empty = all enabled for tenant.</summary>
    public List<AppModuleKey> AllowedModules { get; set; } = [];

    /// <summary>If true, user can see the Cost Bifurcation section on the package form. Set by Tenant Admin when editing users.</summary>
    public bool CanViewCostBifurcation { get; set; }
    /// <summary>
    /// If true, user can override package selling price by entering margin on top of base cost.
    /// Mutually exclusive with <see cref="CanViewCostBifurcation"/>.
    /// </summary>
    public bool CanPriceOverride { get; set; }

    // Stored as PBKDF2 hash string (see PasswordHasher)
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Encrypted password for admin view. Null if never set or encryption unavailable.</summary>
    public string? PasswordEncrypted { get; set; }

    /// <summary>When false, web app idle/active time is not recorded for this user.</summary>
    public bool ActivityTrackingEnabled { get; set; } = true;

    /// <summary>Include this user in inbound lead auto-assignment (sales team).</summary>
    public bool ParticipateInInboundAutoAssign { get; set; }

    /// <summary>Max inbound leads auto-assigned per calendar day (UTC). Ignored when not participating.</summary>
    public int InboundDailyLeadQuota { get; set; }

    /// <summary>Lead sources this user may receive when auto-assigning. Empty = all sources.</summary>
    public List<LeadSource> InboundAllowedLeadSources { get; set; } = [];
}

