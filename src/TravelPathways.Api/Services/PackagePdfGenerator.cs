using PuppeteerSharp;
using PuppeteerSharp.Media;
using System.Text;
using System.Text.RegularExpressions;

namespace TravelPathways.Api.Services;

public sealed class PackagePdfGenerator : IPackagePdfGenerator
{
    private readonly IChromiumBrowserProvider _browserProvider;
    private readonly IConfiguration _configuration;

    public PackagePdfGenerator(IChromiumBrowserProvider browserProvider, IConfiguration configuration)
    {
        _browserProvider = browserProvider;
        _configuration = configuration;
    }

    public async Task<byte[]> GenerateAsync(PackagePdfModel model, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model.CustomHtmlTemplate))
            throw new InvalidOperationException(
                "No PDF HtmlTemplate is configured. Assign an active PDF template with HTML content to this tenant.");

        var html = BuildCustomHtml(model);
        var timeoutMs = 60000; // 60s default
        var timeoutConfig = _configuration["PdfGenerator:TimeoutSeconds"]?.Trim() ?? _configuration["PdfGenerator__TimeoutSeconds"]?.Trim();
        if (!string.IsNullOrEmpty(timeoutConfig) && int.TryParse(timeoutConfig, out var seconds) && seconds > 0)
            timeoutMs = Math.Min(seconds * 1000, 300000); // cap 5 min

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeoutMs);

        return await _browserProvider.RunWithPageAsync(async page =>
        {
            await page.SetJavaScriptEnabledAsync(false);
            await page.SetContentAsync(html, new NavigationOptions { WaitUntil = new[] { WaitUntilNavigation.Load } });
            return await page.PdfDataAsync(new PdfOptions
            {
                Format = PaperFormat.A4,
                PrintBackground = true,
                MarginOptions = new MarginOptions
                {
                    Top = "18mm",
                    Right = "15mm",
                    Bottom = "18mm",
                    Left = "15mm"
                }
            });
        }, cts.Token).ConfigureAwait(false);
    }

    private static readonly Regex PlaceholderRegex = new(@"\{\{[^{}]+\}\}", RegexOptions.Compiled);

    /// <summary>Single pass over the template — avoids O(tokens × length) repeated <see cref="string.Replace"/> on large HTML.</summary>
    private static string ApplyPlaceholders(string template, Dictionary<string, string> replacements) =>
        PlaceholderRegex.Replace(template, m =>
            replacements.TryGetValue(m.Value, out var v) ? v : m.Value);

    private static string H(string? s) => string.IsNullOrEmpty(s) ? "" : System.Net.WebUtility.HtmlEncode(s);

    private static string FormatHotelNameWithArea(string name, string? area)
    {
        var n = (name ?? "").Trim();
        var a = (area ?? "").Trim();
        if (a is "" or "–") return string.IsNullOrEmpty(n) ? "–" : n;
        if (string.IsNullOrEmpty(n)) return a;
        return $"{n}, {a}";
    }

    private static string BuildCustomHtml(PackagePdfModel m)
    {
        var template = m.CustomHtmlTemplate ?? "";
        if (string.IsNullOrWhiteSpace(template))
            throw new InvalidOperationException("PDF HtmlTemplate body is empty.");

        template = RemoveLegacySectionHeadings(template);

        var primaryCss = SanitizeCssColor(m.PrimaryColor) ?? "#3366cc";
        var secondaryCss = SanitizeCssColor(m.SecondaryColor) ?? "#6699ff";
        var coverTitleDisplay = string.IsNullOrWhiteSpace(m.CoverTitle) ? "Holiday Quote" : m.CoverTitle.Trim();

        static string SafeImgSrc(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return "";
            return url.IndexOf('"') >= 0 ? url.Replace("\"", "&quot;") : url;
        }

        var agencyLogoUrl = SafeImgSrc(m.AgencyLogoUrl);
        var agencyLogoHtml = string.IsNullOrWhiteSpace(m.AgencyLogoUrl)
            ? ""
            : $"<img src=\"{agencyLogoUrl}\" alt=\"\" style=\"max-height:56px;max-width:220px;object-fit:contain;\" />";

        string hotelsHtml = string.Join("", (m.Hotels ?? []).Select(h =>
        {
            var hb = new StringBuilder();
            hb.Append("<div class=\"acc-item\"><div class=\"acc-head\"><div class=\"acc-name\">").Append(H(FormatHotelNameWithArea(h.Name, h.Location))).Append("</div><div class=\"pill\">").Append(h.IsHouseboat ? "Houseboat" : "Hotel").Append("</div></div>");
            hb.Append("<div class=\"acc-meta\">");
            if (h.StarRating > 0) hb.Append(RenderStars(h.StarRating)).Append(" &bull; ");
            hb.Append(" &bull; ").Append(H(h.MealPlan)).Append(" &bull; ").Append(h.Nights.ToString()).Append(" Night(s)</div>");
            hb.Append("<div class=\"acc-divider\"></div>");
            hb.Append("<div class=\"acc-meta\">Rooms: ").Append(m.FirstDayRooms).Append(" &bull; Extra bed: ").Append(h.ExtraBedCount).Append(" &bull; CNB: ").Append(h.CnbCount).Append("</div>");
            if (h.ImageUrls?.Count > 0)
            {
                hb.Append("<div class=\"acc-imgs\">");
                for (var i = 0; i < Math.Min(2, h.ImageUrls.Count); i++)
                {
                    var url = h.ImageUrls[i];
                    if (string.IsNullOrWhiteSpace(url)) continue;
                    var safeSrc = url.IndexOf('"') >= 0 ? url.Replace("\"", "&quot;") : url;
                    hb.Append("<img src=\"").Append(safeSrc).Append("\" alt=\"\" />");
                }
                hb.Append("</div>");
            }
            hb.Append("</div>");
            return hb.ToString();
        }));
        string daysHtml = string.Join("", (m.Days ?? []).Select(d =>
        {
            var paxParts = new List<string>();
            if (d.ExtraBedCount > 0) paxParts.Add($"Extra bed: {d.ExtraBedCount}");
            if (d.CnbCount > 0) paxParts.Add($"CNB: {d.CnbCount}");
            var paxHtml = paxParts.Count == 0
                ? ""
                : "<div class=\"day-pax\">" + string.Join(" &bull; ", paxParts.Select(x => H(x))) + "</div>";
            return $"<div class=\"day-block\"><div class=\"day-head\"><span class=\"day-no\">{d.DayNumber}</span><span class=\"day-title\">{d.DayNumber} Day &bull; {H(d.Title)}</span></div><div class=\"day-desc\">{H(d.Description)}</div>{paxHtml}</div>";
        }));
        string accommodationHtml = string.Join("", (m.Days ?? [])
            .Where(d => !string.IsNullOrWhiteSpace(d.HotelName))
            .Select(d =>
            {
                var dateText = string.IsNullOrWhiteSpace(d.DateLabel) ? "" : $" - {H(d.DateLabel)}";
                return $"<div class=\"acc-day-line\"><strong>Day {d.DayNumber}{dateText}</strong><div>Hotel name: {H(d.HotelName)}</div><div>Hotel area: {H(d.HotelLocation)}</div></div>";
            }));
        // Itinerary overview: day + description only (no accommodation block, no hotel images).
        // Structure: .itv-head with .itv-day-pill + optional .itv-date — styled per HtmlTemplate.
        string itineraryOverviewHtml = string.Join("", (m.Days ?? []).Select(d =>
        {
            var dateHtml = string.IsNullOrWhiteSpace(d.DateLabel)
                ? ""
                : $"<span class=\"itv-date\">{H(d.DateLabel)}</span>";
            var paxParts = new List<string>();
            //if (d.ExtraBedCount > 0) paxParts.Add($"Extra bed: {d.ExtraBedCount}");
            //if (d.CnbCount > 0) paxParts.Add($"CNB: {d.CnbCount}");
            var paxHtml = paxParts.Count == 0
                ? ""
                : "<div class=\"itv-pax\">" + string.Join(" &bull; ", paxParts.Select(x => H(x))) + "</div>";
            return $"<div class=\"itv-card\"><div class=\"itv-head\"><span class=\"itv-day-pill\">Day {d.DayNumber}</span>{dateHtml}</div><div class=\"itv-desc\">{H(d.Description)}</div>{paxHtml}</div>";
        }));
        string inclusionsHtml = string.Join("", (m.InclusionLabels ?? []).Select(x => $"<li>{H(x)}</li>"));
        string exclusionsHtml = string.Join("", (m.ExclusionLabels ?? []).Select(x => $"<li>{H(x)}</li>"));
        string termsHtml = string.Join("", (m.TermsAndConditions ?? []).Select(x => $"<li>{H(x)}</li>"));
        string cancellationHtml = string.Join("", (m.CancellationPolicy ?? []).Select(x => $"<li>{H(x)}</li>"));
        string supplementHtml = string.Join("", (m.SupplementCosts ?? []).Select(x => $"<li>{H(x)}</li>"));
        string bankHtml = string.Join("", (m.BankAccounts ?? []).Select(b =>
            $"<tr><td>{H(b.AccountHolderName)}</td><td>{H(b.BankName)}</td><td>{H(b.AccountNumber)}</td><td>{H(b.IFSC)}</td></tr>"));
        string qrHtml = string.Join("", (m.QrCodes ?? []).Where(q => !string.IsNullOrWhiteSpace(q.ImageUrl)).Select(q =>
        {
            var safeSrc = q.ImageUrl.IndexOf('"') >= 0 ? q.ImageUrl.Replace("\"", "&quot;") : q.ImageUrl;
            return $"<div style=\"display:inline-block;text-align:center;margin-right:8px;\"><img src=\"{safeSrc}\" style=\"width:90px;height:90px;object-fit:contain;\" alt=\"\" /><div>{H(q.Label)}</div></div>";
        }));

        var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["{{PrimaryColor}}"] = primaryCss,
            ["{{SecondaryColor}}"] = secondaryCss,
            ["{{CoverTitle}}"] = H(coverTitleDisplay),
            ["{{AgencyLogoUrl}}"] = agencyLogoUrl,
            ["{{AgencyLogoHtml}}"] = agencyLogoHtml,
            ["{{PackageName}}"] = H(m.PackageName),
            ["{{ClientName}}"] = H(m.ClientName),
            ["{{ClientPhone}}"] = H(m.ClientPhone),
            ["{{ClientEmail}}"] = H(m.ClientEmail),
            ["{{ClientAddress}}"] = H(m.ClientAddress),
            ["{{StartDate}}"] = H(m.StartDate),
            ["{{EndDate}}"] = H(m.EndDate),
            ["{{DaysLabel}}"] = H(m.DaysLabel),
            ["{{MealPlan}}"] = H(m.MealPlanLabel),
            ["{{PickUpLocation}}"] = H(m.PickUpLocation),
            ["{{DropLocation}}"] = H(m.DropLocation),
            ["{{DestinationLine}}"] = H(m.DropLocation ?? m.PickUpLocation ?? "Jammu & Kashmir"),
            ["{{NumberOfAdults}}"] = H(m.NumberOfAdults.ToString()),
            ["{{NumberOfChildren}}"] = H(m.NumberOfChildren.ToString()),
            ["{{PaxSummary}}"] = H($"{m.NumberOfAdults} Adults{(m.NumberOfChildren > 0 ? $" + {m.NumberOfChildren} Children" : "")}"),
            // Legacy aliases: map to peak extra bed / CNB counts (not child headcount — use NumberOfChildren for that).
            ["{{ChildrenAbove5}}"] = H(m.TotalExtraBeds.ToString()),
            ["{{ChildrenBelow5}}"] = H(m.TotalCnbCount.ToString()),
            ["{{TotalExtraBeds}}"] = H(m.TotalExtraBeds.ToString()),
            ["{{TotalCnbCount}}"] = H(m.TotalCnbCount.ToString()),
            ["{{FinalAmount}}"] = H(m.FinalAmount),
            ["{{TotalAmount}}"] = H(m.TotalAmount),
            ["{{TotalPackagePrice}}"] = H(string.IsNullOrEmpty(m.TotalPackagePrice) ? m.TotalAmount : m.TotalPackagePrice),
            ["{{MarginAmount}}"] = H(m.MarginAmountDisplay),
            ["{{PerPersonAmount}}"] = H(m.PerPersonAmount ?? "–"),
            ["{{Discount}}"] = H(m.Discount),
            ["{{AdvanceAmount}}"] = H(m.AdvanceAmount),
            ["{{BalanceAmount}}"] = H(m.BalanceAmount),
            ["{{AgencyName}}"] = H(m.AgencyName),
            ["{{AgencyPhone}}"] = H(m.AgencyPhone),
            ["{{AgencyEmail}}"] = H(m.AgencyEmail),
            ["{{ManagingDirectorName}}"] = H(m.ManagingDirectorName),
            ["{{GeneratedDate}}"] = H(m.GeneratedDate),
            ["{{HotelsHtml}}"] = hotelsHtml,
            ["{{AccommodationHtml}}"] = accommodationHtml,
            ["{{DaysHtml}}"] = daysHtml,
            ["{{ItineraryOverviewHtml}}"] = itineraryOverviewHtml,
            ["{{InclusionsHtml}}"] = inclusionsHtml,
            ["{{ExclusionsHtml}}"] = exclusionsHtml,
            ["{{TermsHtml}}"] = termsHtml,
            ["{{CancellationHtml}}"] = cancellationHtml,
            ["{{SupplementHtml}}"] = supplementHtml,
            ["{{BankRowsHtml}}"] = bankHtml,
            ["{{QrHtml}}"] = qrHtml
        };

        template = ApplyPlaceholders(template, replacements);

        return template;
    }

    private static string RemoveLegacySectionHeadings(string template)
    {
        if (string.IsNullOrWhiteSpace(template)) return template;

        // Remove full heading + token blocks for legacy standalone sections.
        template = Regex.Replace(
            template,
            @"<h[1-6][^>]*>\s*Day\s*Wise\s*Itinerary\s*</h[1-6]>\s*\{\{\s*DaysHtml\s*\}\}",
            "",
            RegexOptions.IgnoreCase);
        template = Regex.Replace(
            template,
            @"<h[1-6][^>]*>\s*Accommodation\s*</h[1-6]>\s*\{\{\s*AccommodationHtml\s*\}\}",
            "",
            RegexOptions.IgnoreCase);

        // If headings are present without tokens, strip the headings themselves.
        template = Regex.Replace(
            template,
            @"<h[1-6][^>]*>\s*Day\s*Wise\s*Itinerary\s*</h[1-6]>",
            "",
            RegexOptions.IgnoreCase);
        template = Regex.Replace(
            template,
            @"<h[1-6][^>]*>\s*Accommodation\s*</h[1-6]>",
            "",
            RegexOptions.IgnoreCase);

        return template;
    }

    private static string RenderStars(int rating)
    {
        var s = new StringBuilder();
        var full = rating;
        var half = 0;
        if (rating > 4 && rating < 5) { full = 4; half = 1; }
        for (var i = 0; i < full; i++) s.Append("★");
        if (half > 0) s.Append("☆");
        for (var i = full + half; i < 5; i++) s.Append("☆");
        return s.ToString();
    }

    /// <summary>Allow only safe CSS color values (hex or rgb) to avoid injection.</summary>
    private static string? SanitizeCssColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim();
        if (v.Length == 0) return null;
        if (v[0] == '#' && v.Length >= 4 && v.Length <= 9 && v.Skip(1).All(c => char.IsAsciiHexDigit(c)))
            return v;
        if (v.StartsWith("rgb(", StringComparison.OrdinalIgnoreCase) && v.EndsWith(")"))
            return v;
        return null;
    }
}

