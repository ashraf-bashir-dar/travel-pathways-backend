namespace TravelPathways.Api.Common;

/// <summary>Helpers for PostgreSQL ILike search patterns.</summary>
public static class PostgresSearch
{
    /// <summary>Build a case-insensitive contains pattern with % wildcards; escapes \, %, and _.</summary>
    public static string ToContainsPattern(string? term)
    {
        if (string.IsNullOrWhiteSpace(term))
            return "%";

        var escaped = term.Trim()
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
        return $"%{escaped}%";
    }
}
