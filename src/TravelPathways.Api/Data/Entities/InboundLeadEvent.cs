using TravelPathways.Api.Common;

namespace TravelPathways.Api.Data.Entities;

public sealed class InboundLeadEvent : TenantEntityBase
{
  public InboundLeadProvider Provider { get; set; }
  public string? ExternalId { get; set; }
  public string Status { get; set; } = "Received";
  public string? RawPayload { get; set; }
  public string? ErrorMessage { get; set; }
  public Guid? LeadId { get; set; }
  public Lead? Lead { get; set; }
}
