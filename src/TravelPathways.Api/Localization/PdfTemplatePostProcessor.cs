using System.Net;
using System.Text.RegularExpressions;
using TravelPathways.Api.Localization;
using TravelPathways.Api.Services;

namespace TravelPathways.Api.Localization;

/// <summary>Replaces legacy hardcoded English footer/leadership snippets in stored PDF templates.</summary>
public static class PdfTemplatePostProcessor
{
    private static readonly Regex RegisteredOfficeAddressBlock = new(
        @"<p class=""last-page-registered-addr"">\s*[\s\S]*?</p>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RegisteredOfficePhoneSpan = new(
        @"(<span class=""last-page-reg-value last-page-reg-phone"">)\s*[^<]*(\s*</span>)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string Apply(string html, PackagePdfModel model)
    {
        if (string.IsNullOrEmpty(html)) return html;

        if (!string.IsNullOrWhiteSpace(model.RegisteredOfficeAddressHtml))
        {
            html = RegisteredOfficeAddressBlock.Replace(
                html,
                $"<p class=\"last-page-registered-addr\">{model.RegisteredOfficeAddressHtml}</p>");
        }

        if (!string.IsNullOrWhiteSpace(model.AgencyEmail))
        {
            html = html.Replace("info@travelpathways.in", model.AgencyEmail.Trim(), StringComparison.OrdinalIgnoreCase);
            html = html.Replace(
                "mailto:info@travelpathways.in",
                "mailto:" + model.AgencyEmail.Trim(),
                StringComparison.OrdinalIgnoreCase);
        }

        if (!string.IsNullOrWhiteSpace(model.AgencyWebsite))
        {
            html = html.Replace("www.travelpathways.in", model.AgencyWebsite.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        if (!string.IsNullOrWhiteSpace(model.AgencyPhone))
        {
            var phone = WebUtility.HtmlEncode(model.AgencyPhone.Trim());
            // Do not use "$1{phone}$2" — digits after $ are parsed as a group index (e.g. $19…).
            html = RegisteredOfficePhoneSpan.Replace(
                html,
                m => $"{m.Groups[1].Value}{phone}{m.Groups[2].Value}");
            html = html.Replace("<dd>9906372023</dd>", $"<dd>{phone}</dd>", StringComparison.Ordinal);
        }

        if (!string.IsNullOrWhiteSpace(model.ManagingDirectorName) || !string.IsNullOrWhiteSpace(model.SalesHeadName))
        {
            var md = WebUtility.HtmlEncode(model.ManagingDirectorName?.Trim() ?? "–");
            var sales = WebUtility.HtmlEncode(
                model.SalesHeadName?.Trim() ?? model.ManagingDirectorName?.Trim() ?? "–");
            var first = true;
            html = Regex.Replace(
                html,
                "<dd>Mr Ashraf Dar</dd>",
                _ =>
                {
                    if (first)
                    {
                        first = false;
                        return $"<dd>{md}</dd>";
                    }

                    return $"<dd>{sales}</dd>";
                },
                RegexOptions.None);
        }

        html = InjectTransportSectionIfMissing(html, model);
        if (string.Equals(model.TemplateKey, "pdf-client-15-carbon-lime", StringComparison.OrdinalIgnoreCase)
            || string.Equals(model.TemplateKey, "pdf-client-16-crimson-noir", StringComparison.OrdinalIgnoreCase))
            html = InjectItineraryTitleStyleIfMissing(html);

        return html;
    }

    /// <summary>Ensure template title after day pill and date at end are styled in tenant templates that predate .itv-title.</summary>
    private static string InjectItineraryTitleStyleIfMissing(string html)
    {
        const string styleClose = "</style>";
        var idx = html.IndexOf(styleClose, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return html;

        var css = new System.Text.StringBuilder();
        if (!html.Contains(".itv-day-pill + .itv-title", StringComparison.OrdinalIgnoreCase))
        {
            css.Append("""
                .itv-day-pill + .itv-title::before {
                  content: ", ";
                  font-weight: 400;
                }

                """);
        }
        if (!html.Contains(".itv-title", StringComparison.OrdinalIgnoreCase))
        {
            css.Append("""
                .itv-title {
                  font-family: Cambria, Georgia, "Times New Roman", serif;
                  font-size: 11pt;
                  font-weight: 600;
                  color: var(--primary, #1a2f4d);
                  letter-spacing: 0.02em;
                  line-height: 1.25;
                }

                """);
        }
        if (!html.Contains(".itv-title + .itv-date", StringComparison.OrdinalIgnoreCase))
        {
            css.Append("""
                .itv-title + .itv-date {
                  margin-left: auto;
                }

                """);
        }

        if (css.Length == 0) return html;
        return html.Insert(idx, css.ToString());
    }

    /// <summary>Inject transport block for tenant templates that predate {{TransportHtml}}.</summary>
    private static string InjectTransportSectionIfMissing(string html, PackagePdfModel model)
    {
        if (html.Contains("transport-section", StringComparison.OrdinalIgnoreCase)) return html;

        var transportHtml = PackagePdfHtmlFragments.BuildTransportSectionHtml(model);
        if (string.IsNullOrEmpty(transportHtml)) return html;

        const string marker = "<div class=\"pricing-panel\">";
        var idx = html.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            const string altMarker = "<h2 class=\"section-title section-itinerary-overview\">";
            idx = html.IndexOf(altMarker, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return html;
            return html.Insert(idx, transportHtml);
        }

        return html.Insert(idx, transportHtml);
    }

    public static string FormatRegisteredOfficeAddressHtml(string? tenantAddress, PdfLocalizedStrings labels)
    {
        if (!string.IsNullOrWhiteSpace(tenantAddress))
        {
            return WebUtility.HtmlEncode(tenantAddress.Trim())
                .Replace("\r\n", "<br />")
                .Replace("\n", "<br />");
        }

        return labels.DefaultRegisteredOfficeAddressHtml;
    }

    public static string? WebsiteFromEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var at = email.IndexOf('@');
        if (at < 0 || at >= email.Length - 1) return null;
        var domain = email[(at + 1)..].Trim();
        return string.IsNullOrEmpty(domain) ? null : "www." + domain;
    }
}
