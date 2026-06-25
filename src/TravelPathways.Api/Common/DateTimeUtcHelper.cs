namespace TravelPathways.Api.Common;

/// <summary>Normalize dates for PostgreSQL timestamp with time zone (UTC only).</summary>
public static class DateTimeUtcHelper
{
    public static DateTime ToUtcDate(DateTime value) =>
        DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);

    public static DateTime? ToUtcDate(DateTime? value) =>
        value.HasValue ? ToUtcDate(value.Value) : null;

    public static DateTime UtcToday() =>
        ToUtcDate(DateTime.UtcNow);
}
