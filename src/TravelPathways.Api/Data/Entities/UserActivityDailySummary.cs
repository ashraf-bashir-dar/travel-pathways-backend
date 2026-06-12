namespace TravelPathways.Api.Data.Entities;

/// <summary>Daily active vs idle seconds for a user (web app usage).</summary>
public sealed class UserActivityDailySummary : TenantEntityBase
{
    public Guid UserId { get; set; }
    public AppUser? User { get; set; }

    /// <summary>Calendar date (UTC midnight).</summary>
    public DateTime ActivityDate { get; set; }

    public int ActiveSeconds { get; set; }
    public int IdleSeconds { get; set; }

    public bool IsCurrentlyIdle { get; set; }

    public DateTime LastReportedAtUtc { get; set; }
}
