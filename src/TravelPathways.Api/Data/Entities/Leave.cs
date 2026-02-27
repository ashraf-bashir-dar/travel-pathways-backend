using TravelPathways.Api.Common;

namespace TravelPathways.Api.Data.Entities;

/// <summary>Leave request by an employee; admin can approve or reject.</summary>
public sealed class Leave : TenantEntityBase
{
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public LeaveType LeaveType { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public LeaveStatus Status { get; set; }
}
