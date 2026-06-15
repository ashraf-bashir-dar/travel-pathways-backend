using TravelPathways.Api.Common;

namespace TravelPathways.Api.Data.Entities;

/// <summary>Task created by admin with a due date and assigned to an employee.</summary>
public sealed class EmployeeAssignedTask : TenantEntityBase
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly DueDate { get; set; }

    public Guid AssignedToUserId { get; set; }
    public AppUser AssignedToUser { get; set; } = null!;

    public Guid AssignedByUserId { get; set; }
    public AppUser AssignedByUser { get; set; } = null!;

    public EmployeeAssignedTaskStatus Status { get; set; } = EmployeeAssignedTaskStatus.Pending;
    public DateTime? CompletedAtUtc { get; set; }

    /// <summary>Progress notes added by the assignee while working on the task.</summary>
    public string? AssigneeNotes { get; set; }
}
