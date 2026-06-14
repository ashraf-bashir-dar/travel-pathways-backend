namespace TravelPathways.Api.Data.Entities;

public static class UserActivityVisitSource
{
    public const string InApp = "InApp";
    public const string ExternalLink = "ExternalLink";
    public const string Browser = "Browser";
    /// <summary>Automatic log when user is inactive in the app for 15+ minutes.</summary>
    public const string Idle = "Idle";
}
