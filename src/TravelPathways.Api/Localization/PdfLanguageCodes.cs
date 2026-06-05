namespace TravelPathways.Api.Localization;

/// <summary>English plus Indian regional languages supported for package PDFs.</summary>
public static class PdfLanguageCodes
{
    public const string English = "en";
    public const string Hindi = "hi";
    public const string Malayalam = "ml";
    public const string Tamil = "ta";
    public const string Telugu = "te";
    public const string Kannada = "kn";
    public const string Marathi = "mr";

    public static readonly IReadOnlyList<string> Supported =
    [
        English, Hindi, Malayalam, Tamil, Telugu, Kannada, Marathi
    ];

    private static readonly HashSet<string> SupportedSet =
        new(Supported, StringComparer.OrdinalIgnoreCase);

    public static string Normalize(string? language)
    {
        if (string.IsNullOrWhiteSpace(language)) return English;
        var code = language.Trim().ToLowerInvariant();
        var primary = code.Split('-', '_')[0];
        return SupportedSet.Contains(primary) ? primary : English;
    }
}
