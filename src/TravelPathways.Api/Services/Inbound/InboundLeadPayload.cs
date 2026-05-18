using TravelPathways.Api.Common;

namespace TravelPathways.Api.Services.Inbound;

public sealed class InboundLeadPayload
{
    public required string PhoneNumber { get; init; }
    public string ClientName { get; init; } = string.Empty;
    public string? ClientEmail { get; init; }
    public string? ClientState { get; init; }
    public string? ClientCity { get; init; }
    public string Address { get; init; } = string.Empty;
    public LeadSource LeadSource { get; init; } = LeadSource.SocialMedia;
    public string? Notes { get; init; }
    public string? ExternalId { get; init; }
    public InboundLeadProvider Provider { get; init; } = InboundLeadProvider.Generic;
    public string? RawPayload { get; init; }
}

public sealed class InboundLeadProcessResult
{
    public bool Success { get; init; }
    public bool Duplicate { get; init; }
    public Guid? LeadId { get; init; }
    public Guid? AssignedToUserId { get; init; }
    public string? ErrorMessage { get; init; }
}
