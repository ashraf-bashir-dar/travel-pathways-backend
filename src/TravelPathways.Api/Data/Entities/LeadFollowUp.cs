using TravelPathways.Api.Common;

namespace TravelPathways.Api.Data.Entities;

public sealed class LeadFollowUp
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LeadId { get; set; }
    public Lead? Lead { get; set; }

    /// <summary>Date of the follow-up.</summary>
    public DateTime FollowUpDate { get; set; }
    public FollowUpStatus Status { get; set; }
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
}
