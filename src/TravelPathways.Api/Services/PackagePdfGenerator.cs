using Microsoft.Extensions.Configuration;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using System.Text;

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
        var html = BuildHtml(model);
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

    private static string H(string? s) => string.IsNullOrEmpty(s) ? "" : System.Net.WebUtility.HtmlEncode(s);

    private static string BuildHtml(PackagePdfModel m)
    {
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"UTF-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        // No external fonts: avoids network latency/timeouts in Docker/Web App; use system fonts only
        sb.Append("<style>");
        sb.Append("*{box-sizing:border-box;} body{margin:0;font-family:Arial,Helvetica,sans-serif;font-size:10pt;line-height:1.5;color:#000;}");
        sb.Append("/* Page 1: cover + details */");
        sb.Append(".page-1{padding:0;min-height:100vh;}");
        sb.Append(".cover-block{text-align:center;padding:1.25rem 0 1.5rem;border-bottom:2px solid #3366cc;margin-bottom:1.25rem;}");
        sb.Append(".cover-title{font-size:1.1rem;font-weight:700;color:#3366cc;letter-spacing:0.02em;margin:0 0 0.25rem;}");
        sb.Append(".cover-by{font-size:0.95rem;color:#475569;margin:0 0 0.6rem;font-style:italic;}");
        sb.Append(".cover-package{font-size:1.35rem;font-weight:700;color:#1e293b;margin:0 0 0.4rem;line-height:1.3;}");
        sb.Append(".cover-meta{font-size:0.9rem;color:#64748b;margin:0 0 0.35rem;}");
        sb.Append(".cover-for{font-size:0.95rem;color:#334155;margin:0.5rem 0 0;}");
        sb.Append(".cover-date{font-size:0.8rem;color:#94a3b8;margin-top:0.5rem;}");
        sb.Append("/* Itinerary */");
        sb.Append(".itin-title{font-size:1.15rem;font-weight:700;margin:1.5rem 0 1rem;text-align:center;}");
        sb.Append(".itin-title span{border-bottom:2px dashed #333;padding-bottom:2px;}");
        sb.Append(".day-block{page-break-inside:avoid;margin-bottom:1rem;}");
        sb.Append(".day-bar{background:#3366cc;color:#fff;padding:0.5rem 1rem;display:flex;align-items:center;gap:0.6rem;}");
        sb.Append(".day-circle{width:36px;height:36px;background:#6699ff;border-radius:50%;display:flex;align-items:center;justify-content:center;font-weight:700;font-size:0.9rem;}");
        sb.Append(".day-bar-title{font-weight:700;}");
        sb.Append(".day-para{margin:0.5rem 0 0;padding:0.85rem 0.6rem;min-height:4rem;line-height:1.6;font-size:10pt;}");
        sb.Append("/* Accommodation */");
        sb.Append(".acc-card{page-break-inside:avoid;margin-bottom:1.5rem;}");
        sb.Append(".acc-banner{background:#3366cc;color:#fff;text-align:center;padding:0.4rem;font-weight:700;font-size:0.95rem;}");
        sb.Append(".acc-name{font-size:1.2rem;font-weight:700;margin:0.5rem 0 0.25rem;}");
        sb.Append(".acc-loc{font-size:0.9rem;color:#333;}");
        sb.Append(".acc-meta{display:flex;align-items:center;gap:1rem;margin:0.5rem 0;flex-wrap:wrap;}");
        sb.Append(".acc-stars{color:#000;}");
        sb.Append(".acc-services{font-size:0.85rem;margin:0.5rem 0;}");
        sb.Append(".acc-imgs{display:grid;grid-template-columns:1fr 1fr;gap:10px;margin-top:0.75rem;}");
        sb.Append(".acc-imgs img{width:100%;height:220px;object-fit:cover;border-radius:8px;}");
        sb.Append("/* Client Info & Night Stays */");
        sb.Append(".section-head{font-size:1.05rem;font-weight:700;background:#3366cc;color:#fff;margin:1.25rem 0 0.5rem;padding:0.4rem 0.5rem;border:none;}");
        sb.Append(".info-row{display:flex;margin-bottom:4px;}");
        sb.Append(".info-tab{width:10px;background:#3366cc;border-radius:6px 0 0 6px;flex-shrink:0;}");
        sb.Append(".info-tab-r{width:10px;background:#3366cc;border-radius:0 6px 6px 0;flex-shrink:0;}");
        sb.Append(".info-cell{flex:1;background:#e5e7eb;padding:0.45rem 0.65rem;font-size:0.9rem;}");
        sb.Append(".info-cell.val{text-align:center;}");
        sb.Append(".info-header .info-cell{background:#3366cc;color:#fff;font-weight:600;}");
        sb.Append("/* Page 1: each section in its own row (full width) */");
        sb.Append(".page-1-section{margin-bottom:1.25rem;}");
        sb.Append("/* Inclusion / Excludes */");
        sb.Append(".inc-bar{background:#3366cc;color:#fff;padding:0.5rem 0.75rem;font-weight:700;}");
        sb.Append(".inclusion-section{margin-top:2rem;}");
        sb.Append(".inc-sub{font-size:0.9rem;margin:0.35rem 0 0;}");
        sb.Append(".inc-list{margin:0.5rem 0;padding-left:1.25rem;} .inc-list li{margin:0.2rem 0;list-style-type:square;}");
        sb.Append(".exc-bar{background:#3366cc;color:#fff;padding:0.5rem 0.75rem;font-weight:700;}");
        sb.Append(".exc-box{background:#e5e7eb;padding:0.75rem 1rem;border-radius:6px;margin-top:0;}");
        sb.Append(".exc-box .inc-list{margin:0.25rem 0;}");
        sb.Append("/* Travel Agency Details (last page) */");
        sb.Append(".agency-grid{margin-top:0.5rem;border:1px solid #d1d5db;border-radius:6px;overflow:hidden;}");
        sb.Append(".agency-row{display:flex;}");
        sb.Append(".agency-cell{flex:1;padding:0.45rem 0.65rem;font-size:0.9rem;border-bottom:1px solid #e5e7eb;}");
        sb.Append(".agency-row:last-child .agency-cell{border-bottom:none;}");
        sb.Append(".agency-row.info-header .agency-cell{background:#3366cc;color:#fff;font-weight:600;}");
        sb.Append(".agency-row:not(.info-header) .agency-cell{background:#e5e7eb;}");
        sb.Append("@media print{.acc-card,.day-block{page-break-inside:avoid;}}");
        sb.Append("</style></head><body>");

        // —— Page 1: Cover block + Details and Pricing in one row ——
        var showClientPhone = !string.IsNullOrEmpty(m.ClientPhone) && !IsAllZeros(m.ClientPhone);
        sb.Append("<div class=\"page-1\">");
        sb.Append("<div class=\"cover-block\">");
        sb.Append("<p class=\"cover-title\">Kashmir Tour Package Proposal</p>");
        if (!string.IsNullOrWhiteSpace(m.AgencyName))
            sb.Append("<p class=\"cover-by\">Presented by ").Append(H(m.AgencyName.Trim())).Append("</p>");
        sb.Append("<p class=\"cover-meta\">").Append(H(m.DaysLabel)).Append(" &bull; ").Append(H(m.StartDate)).Append(" – ").Append(H(m.EndDate)).Append("</p>");
        sb.Append("<p class=\"cover-for\">Prepared for ").Append(H(m.ClientName)).Append("</p>");
        sb.Append("<p class=\"cover-date\">Proposal date: ").Append(H(m.GeneratedDate)).Append("</p>");
        sb.Append("</div>");
        sb.Append("<div class=\"page-1-section\">");
        sb.Append("<h2 class=\"section-head\">Client information</h2>");
        sb.Append("<div class=\"info-row info-header\"><div class=\"info-tab\"></div><div class=\"info-cell\">Detail</div><div class=\"info-cell val\">Value</div><div class=\"info-tab-r\"></div></div>");
        sb.Append("<div class=\"info-row\"><div class=\"info-tab\"></div><div class=\"info-cell\">Client name</div><div class=\"info-cell val\">").Append(H(m.ClientName)).Append("</div><div class=\"info-tab-r\"></div></div>");
        if (showClientPhone) sb.Append("<div class=\"info-row\"><div class=\"info-tab\"></div><div class=\"info-cell\">Phone</div><div class=\"info-cell val\">").Append(H(m.ClientPhone)).Append("</div><div class=\"info-tab-r\"></div></div>");
        if (!string.IsNullOrWhiteSpace(m.ClientAddress)) sb.Append("<div class=\"info-row\"><div class=\"info-tab\"></div><div class=\"info-cell\">Address</div><div class=\"info-cell val\">").Append(H(m.ClientAddress)).Append("</div><div class=\"info-tab-r\"></div></div>");
        if (!string.IsNullOrWhiteSpace(m.ClientEmail)) sb.Append("<div class=\"info-row\"><div class=\"info-tab\"></div><div class=\"info-cell\">Email</div><div class=\"info-cell val\">").Append(H(m.ClientEmail)).Append("</div><div class=\"info-tab-r\"></div></div>");
        sb.Append("</div>");
        sb.Append("<div class=\"page-1-section\">");
        sb.Append("<h2 class=\"section-head\">Package information</h2>");
        sb.Append("<div class=\"info-row info-header\"><div class=\"info-tab\"></div><div class=\"info-cell\">Detail</div><div class=\"info-cell val\">Value</div><div class=\"info-tab-r\"></div></div>");
        sb.Append("<div class=\"info-row\"><div class=\"info-tab\"></div><div class=\"info-cell\">Package name</div><div class=\"info-cell val\">").Append(H(m.PackageName)).Append("</div><div class=\"info-tab-r\"></div></div>");
        sb.Append("<div class=\"info-row\"><div class=\"info-tab\"></div><div class=\"info-cell\">Duration</div><div class=\"info-cell val\">").Append(H(m.DaysLabel)).Append("</div><div class=\"info-tab-r\"></div></div>");
        sb.Append("<div class=\"info-row\"><div class=\"info-tab\"></div><div class=\"info-cell\">Arrival date</div><div class=\"info-cell val\">").Append(H(m.StartDate)).Append("</div><div class=\"info-tab-r\"></div></div>");
        sb.Append("<div class=\"info-row\"><div class=\"info-tab\"></div><div class=\"info-cell\">Departure date</div><div class=\"info-cell val\">").Append(H(m.EndDate)).Append("</div><div class=\"info-tab-r\"></div></div>");
        sb.Append("<div class=\"info-row\"><div class=\"info-tab\"></div><div class=\"info-cell\">Number of adults</div><div class=\"info-cell val\">").Append(m.NumberOfAdults.ToString().PadLeft(2, '0')).Append("</div><div class=\"info-tab-r\"></div></div>");
        sb.Append("<div class=\"info-row\"><div class=\"info-tab\"></div><div class=\"info-cell\">Number of children</div><div class=\"info-cell val\">").Append(m.NumberOfChildren > 0 ? m.NumberOfChildren.ToString().PadLeft(2, '0') : "NA").Append("</div><div class=\"info-tab-r\"></div></div>");
        if (!string.IsNullOrWhiteSpace(m.PickUpLocation)) sb.Append("<div class=\"info-row\"><div class=\"info-tab\"></div><div class=\"info-cell\">Pick Up Location</div><div class=\"info-cell val\">").Append(H(m.PickUpLocation)).Append("</div><div class=\"info-tab-r\"></div></div>");
        if (!string.IsNullOrWhiteSpace(m.DropLocation)) sb.Append("<div class=\"info-row\"><div class=\"info-tab\"></div><div class=\"info-cell\">Drop Location</div><div class=\"info-cell val\">").Append(H(m.DropLocation)).Append("</div><div class=\"info-tab-r\"></div></div>");
        sb.Append("<div class=\"info-row\"><div class=\"info-tab\"></div><div class=\"info-cell\">No. of Rooms</div><div class=\"info-cell val\">").Append(m.FirstDayRooms.ToString().PadLeft(2, '0')).Append("</div><div class=\"info-tab-r\"></div></div>");
        sb.Append("<div class=\"info-row\"><div class=\"info-tab\"></div><div class=\"info-cell\">Meal Plan</div><div class=\"info-cell val\">").Append(H(m.MealPlanLabel)).Append("</div><div class=\"info-tab-r\"></div></div>");
        sb.Append("</div>");
        sb.Append("<div class=\"page-1-section\">");
        sb.Append("<h2 class=\"section-head\">Pricing</h2>");
        sb.Append("<div class=\"info-row\"><div class=\"info-tab\"></div><div class=\"info-cell\">Total</div><div class=\"info-cell val\">").Append(H(m.TotalAmount)).Append("</div><div class=\"info-tab-r\"></div></div>");
        sb.Append("<div class=\"info-row\"><div class=\"info-tab\"></div><div class=\"info-cell\">Final amount</div><div class=\"info-cell val\">").Append(H(m.FinalAmount)).Append("</div><div class=\"info-tab-r\"></div></div>");
        if (!string.IsNullOrEmpty(m.PerPersonAmount)) sb.Append("<div class=\"info-row\"><div class=\"info-tab\"></div><div class=\"info-cell\">Per Person cost</div><div class=\"info-cell val\">").Append(H(m.PerPersonAmount)).Append("</div><div class=\"info-tab-r\"></div></div>");
        sb.Append("<div class=\"info-row\"><div class=\"info-tab\"></div><div class=\"info-cell\">Advance</div><div class=\"info-cell val\">").Append(H(m.AdvanceAmount)).Append("</div><div class=\"info-tab-r\"></div></div>");
        sb.Append("<div class=\"info-row\"><div class=\"info-tab\"></div><div class=\"info-cell\">Balance</div><div class=\"info-cell val\">").Append(H(m.BalanceAmount)).Append("</div><div class=\"info-tab-r\"></div></div>");
        sb.Append("</div>");
        sb.Append("</div>");

        // —— Itinerary: "XN/YD Itinerary - Day Wise" ——
        sb.Append("<h2 class=\"itin-title\">").Append(H(m.DaysLabel)).Append(" Itinerary - <span>Day Wise</span></h2>");
        foreach (var d in m.Days ?? [])
        {
            sb.Append("<div class=\"day-block\">");
            sb.Append("<div class=\"day-bar\"><span class=\"day-circle\">").Append(d.DayNumber.ToString().PadLeft(2, '0')).Append("</span><span class=\"day-bar-title\">Day ").Append(d.DayNumber.ToString().PadLeft(2, '0')).Append(": ").Append(H(d.Title)).Append("</span></div>");
            sb.Append("<p class=\"day-para\">").Append(H(d.Description)).Append("</p></div>");
        }

        // —— Accommodation (HOTEL / Houseboat per property) ——
        var hotels = m.Hotels ?? [];
        foreach (var h in hotels)
        {
            sb.Append("<div class=\"acc-card\">");
            sb.Append("<div class=\"acc-banner\">").Append(h.IsHouseboat ? "Houseboat" : "HOTEL").Append("</div>");
            sb.Append("<div class=\"acc-name\">").Append(H(h.Name)).Append("</div>");
            sb.Append("<div class=\"acc-loc\">").Append(H(h.Location)).Append("</div>");
            sb.Append("<div class=\"acc-meta\">");
            if (h.StarRating > 0) sb.Append("<span class=\"acc-stars\">").Append(RenderStars(h.StarRating)).Append("</span>");
            sb.Append("<span>").Append(H(h.MealPlan)).Append(h.Nights > 0 ? " &bull; " + h.Nights + " night(s)" : "").Append("</span></div>");
            sb.Append("<div class=\"acc-services\">Breakfast &amp; Dinner &bull; Wi-Fi Daily &bull; Housekeeping &bull; 24-hour Room Service</div>");
            var urls = h.ImageUrls ?? [];
            if (urls.Count > 0)
            {
                sb.Append("<div class=\"acc-imgs\">");
                for (var i = 0; i < Math.Min(4, urls.Count); i++)
                {
                    var url = urls[i] ?? "";
                    if (url.Length == 0) continue;
                    // Do not HTML-encode img src: data URLs break (base64 uses +/=), and http URLs need & and ?
                    var safeSrc = url.IndexOf('"') >= 0 ? url.Replace("\"", "&quot;") : url;
                    sb.Append("<img src=\"").Append(safeSrc).Append("\" alt=\"\"/>");
                }
                sb.Append("</div>");
            }
            sb.Append("</div>");
        }

        // —— Night Stays ——
        sb.Append("<h2 class=\"section-head\">Night Stays</h2>");
        sb.Append("<div class=\"info-row info-header\"><div class=\"info-tab\"></div><div class=\"info-cell\">Destination</div><div class=\"info-cell val\">Nights</div><div class=\"info-tab-r\"></div></div>");
        foreach (var h in hotels)
        {
            sb.Append("<div class=\"info-row\"><div class=\"info-tab\"></div><div class=\"info-cell\">").Append(H(h.Name)).Append("</div><div class=\"info-cell val\">").Append(h.Nights.ToString().PadLeft(2, '0')).Append("</div><div class=\"info-tab-r\"></div></div>");
        }

        // —— Inclusion (with space above) ——
        var incLabels = m.InclusionLabels ?? [];
        sb.Append("<div class=\"inclusion-section\">");
        sb.Append("<div class=\"inc-bar\">Inclusion</div>");
        sb.Append("<ul class=\"inc-list\">");
        foreach (var label in incLabels) sb.Append("<li>").Append(H(label)).Append("</li>");
        if (incLabels.Count == 0) sb.Append("<li>—</li>");
        sb.Append("</ul>");

        // —— Excludes (bar + grey box) ——
        var excLabels = m.ExclusionLabels ?? [];
        sb.Append("<div class=\"exc-bar\">Excludes</div>");
        sb.Append("<div class=\"exc-box\"><ul class=\"inc-list\">");
        foreach (var label in excLabels) sb.Append("<li>").Append(H(label)).Append("</li>");
        if (excLabels.Count == 0) sb.Append("<li>—</li>");
        sb.Append("</ul></div>");
        sb.Append("</div>");

        // —— Travel Agency Details (last page) ——
        if (!string.IsNullOrWhiteSpace(m.ManagingDirectorName) || !string.IsNullOrWhiteSpace(m.AgencyName) || !string.IsNullOrWhiteSpace(m.AgencyPhone) || !string.IsNullOrWhiteSpace(m.AgencyEmail))
        {
            var mdName = string.IsNullOrWhiteSpace(m.ManagingDirectorName) ? "–" : "Mr. " + m.ManagingDirectorName.Trim();
            sb.Append("<h2 class=\"section-head\">Travel Agency Details</h2>");
            sb.Append("<div class=\"agency-grid\">");
            sb.Append("<div class=\"agency-row info-header\"><div class=\"agency-cell\">Managing Director</div><div class=\"agency-cell\">Travel Agency</div><div class=\"agency-cell\">Contact</div><div class=\"agency-cell\">Email</div></div>");
            sb.Append("<div class=\"agency-row\"><div class=\"agency-cell\">").Append(H(mdName)).Append("</div><div class=\"agency-cell\">").Append(H(m.AgencyName ?? "–")).Append("</div><div class=\"agency-cell\">").Append(H(m.AgencyPhone ?? "–")).Append("</div><div class=\"agency-cell\">").Append(H(m.AgencyEmail ?? "–")).Append("</div></div>");
            sb.Append("</div>");
        }

        sb.Append("</body></html>");
        return sb.ToString();
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

    private static bool IsAllZeros(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return true;
        return s.Trim().All(c => c == '0' || char.IsWhiteSpace(c));
    }
}
