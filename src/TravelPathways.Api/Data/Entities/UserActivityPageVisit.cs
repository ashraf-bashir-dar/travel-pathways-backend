namespace TravelPathways.Api.Data.Entities;

/// <summary>Record of a page visited inside the Travel Pathways web app.</summary>
public sealed class UserActivityPageVisit : TenantEntityBase
{
    public Guid UserId { get; set; }
    public AppUser? User { get; set; }

    /// <summary>App route path (e.g. /leads).</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Full in-app URL including query string.</summary>
    public string Url { get; set; } = string.Empty;

    public string? PageTitle { get; set; }

    /// <summary>InApp, ExternalLink, or Browser (extension).</summary>
    public string Source { get; set; } = UserActivityVisitSource.InApp;

    /// <summary>Time spent on page (browser extension only).</summary>
    public int? DurationSeconds { get; set; }

    public DateTime VisitedAtUtc { get; set; }
}
