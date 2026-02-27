namespace TravelPathways.Api.Data.Entities;

/// <summary>One record per user per calendar day: Time In and Time Out.</summary>
public sealed class Attendance : TenantEntityBase
{
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    /// <summary>Calendar date (UTC date at midnight).</summary>
    public DateTime AttendanceDate { get; set; }

    /// <summary>When the user clicked Time In (UTC).</summary>
    public DateTime? TimeInUtc { get; set; }

    /// <summary>When the user clicked Time Out (UTC).</summary>
    public DateTime? TimeOutUtc { get; set; }
}
