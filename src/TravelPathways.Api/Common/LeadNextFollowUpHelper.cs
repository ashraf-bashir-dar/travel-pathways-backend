namespace TravelPathways.Api.Common;

public static class LeadNextFollowUpHelper
{
    public static bool IsTerminalStatus(LeadStatus status) =>
        status is LeadStatus.Confirmed
            or LeadStatus.Cancelled
            or LeadStatus.NotInterested
            or LeadStatus.AlreadyBooked;

/** Business timezone for follow-up defaults (India). */
    private static readonly TimeZoneInfo BusinessTimeZone = ResolveBusinessTimeZone();

    public static DateOnly Today()
    {
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, BusinessTimeZone);
        return DateOnly.FromDateTime(localNow);
    }

    public static DateOnly DefaultDate() => Today().AddDays(1);

    private static TimeZoneInfo ResolveBusinessTimeZone()
    {
        foreach (var id in new[] { "Asia/Kolkata", "India Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }

        return TimeZoneInfo.Utc;
    }

    public static DateOnly? ParseOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return DateOnly.TryParse(value.Trim(), out var d) ? d : null;
    }

    public static DateOnly? ForNewLead(string? requested) =>
        ParseOptional(requested) ?? DefaultDate();

    public static DateOnly? ForStatus(LeadStatus status, string? requested)
    {
        if (IsTerminalStatus(status)) return null;
        return ParseOptional(requested) ?? DefaultDate();
    }

    public static string? ToApiString(DateOnly? date) =>
        date?.ToString("yyyy-MM-dd");
}
