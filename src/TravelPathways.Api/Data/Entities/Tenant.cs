using TravelPathways.Api.Common;

namespace TravelPathways.Api.Data.Entities;

public sealed class Tenant : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty; // short tenant code like "ABC"
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }

    // Optional: a URL/path to logo (served as static file)
    public string? LogoUrl { get; set; }

    // Enabled modules for this tenant (stored as JSON string)
    public List<AppModuleKey> EnabledModules { get; set; } = [];

    public bool IsActive { get; set; } = true;

    public List<TenantDocument> Documents { get; set; } = [];

    // --- Subscription (seat-based, INR) ---
    /// <summary>User who can still log in when subscription is expired (e.g. to renew).</summary>
    public Guid? DefaultUserId { get; set; }
    public AppUser? DefaultUser { get; set; }

    public Guid? PlanId { get; set; }
    public Plan? Plan { get; set; }

    public BillingCycle? BillingCycle { get; set; }
    /// <summary>Number of paid seats (active users cannot exceed this).</summary>
    public int SeatsPurchased { get; set; }
    public SubscriptionStatus SubscriptionStatus { get; set; } = SubscriptionStatus.Active;
    public DateTime? SubscriptionStartUtc { get; set; }
    public DateTime? SubscriptionEndUtc { get; set; }
}

public sealed class TenantDocument : EntityBase
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public TenantDocumentType Type { get; set; }
    public string FileName { get; set; } = string.Empty;

    // Stored relative URL/path, e.g. "/uploads/tenants/{tenantId}/file.pdf"
    public string Url { get; set; } = string.Empty;
}

