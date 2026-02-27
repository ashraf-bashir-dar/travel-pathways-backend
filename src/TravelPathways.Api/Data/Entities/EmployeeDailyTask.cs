namespace TravelPathways.Api.Data.Entities;

/// <summary>Task done by an employee on a given day (for daily monitoring).</summary>
public sealed class EmployeeDailyTask : TenantEntityBase
{
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    /// <summary>Date the task was done (stored as UTC date at midnight).</summary>
    public DateTime TaskDate { get; set; }

    public string Description { get; set; } = string.Empty;

    /// <summary>Order when displaying multiple tasks for the same day (lower first).</summary>
    public int DisplayOrder { get; set; }

    /// <summary>Start time for this task (UTC). Must be same calendar day as TaskDate. Max 2 hours per task.</summary>
    public DateTime? StartTimeUtc { get; set; }

    /// <summary>End time for this task (UTC). Must be same calendar day as TaskDate; EndTimeUtc - StartTimeUtc &lt;= 2 hours.</summary>
    public DateTime? EndTimeUtc { get; set; }
}
