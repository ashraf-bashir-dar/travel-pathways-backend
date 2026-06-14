using System.Globalization;
using TravelPathways.Api.Data.Entities;

namespace TravelPathways.Api.Common;

public static class UserShiftTimeHelper
{
    public static string? Format(TimeOnly? value) =>
        value.HasValue ? value.Value.ToString("HH:mm", CultureInfo.InvariantCulture) : null;

    public static bool TryApplyShiftTimes(
        AppUser user,
        string? shiftStartTime,
        string? shiftEndTime,
        out string? error)
    {
        error = null;

        if (!TryParseOptional(shiftStartTime, out var start, out error))
            return false;

        if (!TryParseOptional(shiftEndTime, out var end, out error))
            return false;

        user.ShiftStartTime = start;
        user.ShiftEndTime = end;
        return true;
    }

    private static bool TryParseOptional(string? value, out TimeOnly? result, out string? error)
    {
        result = null;
        error = null;

        if (string.IsNullOrWhiteSpace(value))
            return true;

        if (TimeOnly.TryParse(value.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            result = parsed;
            return true;
        }

        error = "Shift time must be in HH:mm format (e.g. 09:00).";
        return false;
    }
}
