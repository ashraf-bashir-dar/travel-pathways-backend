using System.Globalization;
using System.Text.RegularExpressions;

namespace TravelPathways.Api.Localization;

/// <summary>Replaces English phrases in stored HTML templates (works for all client PDF templates in DB).</summary>
public static class PdfTemplateLocalizer
{
    private static readonly Regex PlaceholderToken = new(@"\{\{[^{}]+\}\}", RegexOptions.Compiled);
    private static readonly Regex ProtectedPlaceholder = new(@"__TPPDF_PH_(\d+)__", RegexOptions.Compiled);

    public static string Localize(string html, PdfLocalizedStrings labels)
    {
        if (string.IsNullOrEmpty(html) || labels.LanguageCode == PdfLanguageCodes.English)
            return html;

        var tokens = new List<string>();
        var result = ProtectPlaceholders(html, tokens);

        foreach (var (english, localized) in labels.GetTemplateReplacements())
            result = result.Replace(english, localized, StringComparison.Ordinal);

        result = RestorePlaceholders(result, tokens);
        result = result.Replace("<html lang=\"en\">", $"<html lang=\"{labels.LanguageCode}\">", StringComparison.OrdinalIgnoreCase);
        return result;
    }

    /// <summary>Masks <c>{{Token}}</c> values so label translation (e.g. Phone → फ़ोन) cannot corrupt <c>{{ClientPhone}}</c>.</summary>
    private static string ProtectPlaceholders(string html, List<string> tokens) =>
        PlaceholderToken.Replace(html, m =>
        {
            var index = tokens.Count;
            tokens.Add(m.Value);
            return $"__TPPDF_PH_{index.ToString(CultureInfo.InvariantCulture)}__";
        });

    private static string RestorePlaceholders(string html, IReadOnlyList<string> tokens) =>
        ProtectedPlaceholder.Replace(html, m =>
        {
            if (!int.TryParse(m.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var i))
                return m.Value;
            return i >= 0 && i < tokens.Count ? tokens[i] : m.Value;
        });
}
