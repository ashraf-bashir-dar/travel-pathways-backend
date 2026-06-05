using TravelPathways.Api.Common;

namespace TravelPathways.Api.Data.Entities;

public sealed class Lead : TenantEntityBase
{
    public string ClientName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? ClientEmail { get; set; }
    public string? ClientState { get; set; }
    public string? ClientCity { get; set; }

    public string Address { get; set; } = string.Empty;
    public LeadSource LeadSource { get; set; } = LeadSource.Other;
    public LeadStatus Status { get; set; } = LeadStatus.New;
    public bool IsLocked { get; set; }
    public string? Notes { get; set; }

    public Guid? AssignedToUserId { get; set; }
    public AppUser? AssignedToUser { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>Integration provider when created via inbound webhook (null for manual/import).</summary>
    public InboundLeadProvider? InboundProvider { get; set; }

    /// <summary>Provider-specific id for deduplication (e.g. Meta leadgen_id).</summary>
    public string? InboundExternalId { get; set; }
}

