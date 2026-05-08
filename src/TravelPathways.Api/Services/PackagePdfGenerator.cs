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
        var html = !string.IsNullOrWhiteSpace(model.CustomHtmlTemplate)
            ? BuildCustomHtml(model)
            : NormalizeTemplateKey(model.TemplateKey) switch
            {
                "modern-itinerary" => BuildHtml(model),
                _ => BuildHtmlV2(model) // default = classic quote
            };
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

    private static string NormalizeTemplateKey(string? value)
    {
        var v = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (v is "modern" or "modern-itinerary") return "modern-itinerary";
        if (v is "classic" or "classic-quote") return "classic-quote";
        return "classic-quote";
    }

    private static string BuildHtml(PackagePdfModel m)
    {
        var primary = SanitizeCssColor(m.PrimaryColor) ?? "#3366cc";
        var secondary = SanitizeCssColor(m.SecondaryColor) ?? "#6699ff";
        var coverTitle = string.IsNullOrWhiteSpace(m.CoverTitle) ? "Kashmir Tour Package Proposal" : m.CoverTitle.Trim();
        var showBank = m.ShowBankDetails ?? true;
        var showQr = m.ShowQrCodes ?? true;

        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"UTF-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        sb.Append("<style>");
        sb.Append("*{box-sizing:border-box;} body{margin:0;font-family:Arial,Helvetica,sans-serif;font-size:10pt;line-height:1.5;color:#000;}");
        sb.Append(".page-1{padding:0;min-height:100vh;}");
        sb.Append(".cover-block{text-align:center;padding:1.25rem 0 1.5rem;border-bottom:2px solid ").Append(primary).Append(";margin-bottom:1.25rem;}");
        sb.Append(".cover-logo{margin:0 0 0.5rem;}");
        sb.Append(".cover-logo img{max-height:56px;max-width:200px;object-fit:contain;}");
        sb.Append(".cover-title{font-size:1.1rem;font-weight:700;color:").Append(primary).Append(";letter-spacing:0.02em;margin:0 0 0.25rem;}");
        sb.Append(".cover-by{font-size:0.95rem;color:#475569;margin:0 0 0.6rem;font-style:italic;}");
        sb.Append(".cover-package{font-size:1.35rem;font-weight:700;color:#1e293b;margin:0 0 0.4rem;line-height:1.3;}");
        sb.Append(".cover-meta{font-size:0.9rem;color:#64748b;margin:0 0 0.35rem;}");
        sb.Append(".cover-for{font-size:0.95rem;color:#334155;margin:0.5rem 0 0;}");
        sb.Append(".cover-date{font-size:0.8rem;color:#94a3b8;margin-top:0.5rem;}");
        sb.Append(".itin-title{font-size:1.15rem;font-weight:700;margin:1.5rem 0 1rem;text-align:center;}");
        sb.Append(".itin-title span{border-bottom:2px dashed #333;padding-bottom:2px;}");
        sb.Append(".day-block{page-break-inside:avoid;margin-bottom:1rem;}");
        sb.Append(".day-bar{background:").Append(primary).Append(";color:#fff;padding:0.5rem 1rem;display:flex;align-items:center;gap:0.6rem;}");
        sb.Append(".day-circle{width:36px;height:36px;background:").Append(secondary).Append(";border-radius:50%;display:flex;align-items:center;justify-content:center;font-weight:700;font-size:0.9rem;}");
        sb.Append(".day-bar-title{font-weight:700;}");
        sb.Append(".day-para{margin:0.5rem 0 0;padding:0.85rem 0.6rem;min-height:4rem;line-height:1.6;font-size:10pt;}");
        sb.Append(".day-extra{margin:0.35rem 0 0;padding:0 0.6rem;font-size:9pt;color:#475569;}");
        sb.Append(".acc-card{page-break-inside:avoid;margin-bottom:1.5rem;}");
        sb.Append(".acc-banner{background:").Append(primary).Append(";color:#fff;text-align:center;padding:0.4rem;font-weight:700;font-size:0.95rem;}");
        sb.Append(".acc-name{font-size:1.2rem;font-weight:700;margin:0.5rem 0 0.25rem;}");
        sb.Append(".acc-loc{font-size:0.9rem;color:#333;}");
        sb.Append(".acc-meta{display:flex;align-items:center;gap:1rem;margin:0.5rem 0;flex-wrap:wrap;}");
        sb.Append(".acc-stars{color:#000;}");
        sb.Append(".acc-services{font-size:0.85rem;margin:0.5rem 0;}");
        sb.Append(".acc-imgs{display:grid;grid-template-columns:1fr 1fr;gap:10px;margin-top:0.75rem;}");
        sb.Append(".acc-imgs img{width:100%;height:220px;object-fit:cover;border-radius:8px;}");
        sb.Append(".section-head{font-size:1.05rem;font-weight:700;background:").Append(primary).Append(";color:#fff;margin:1.25rem 0 0.5rem;padding:0.4rem 0.5rem;border:none;}");
        sb.Append(".info-row{display:flex;margin-bottom:4px;}");
        sb.Append(".info-tab{width:10px;background:").Append(primary).Append(";border-radius:6px 0 0 6px;flex-shrink:0;}");
        sb.Append(".info-tab-r{width:10px;background:").Append(primary).Append(";border-radius:0 6px 6px 0;flex-shrink:0;}");
        sb.Append(".info-cell{flex:1;background:#e5e7eb;padding:0.45rem 0.65rem;font-size:0.9rem;}");
        sb.Append(".info-cell.val{text-align:center;}");
        sb.Append(".info-header .info-cell{background:").Append(primary).Append(";color:#fff;font-weight:600;}");
        sb.Append(".page-1-section{margin-bottom:1.25rem;}");
        sb.Append(".inc-bar{background:").Append(primary).Append(";color:#fff;padding:0.5rem 0.75rem;font-weight:700;}");
        sb.Append(".inclusion-section{margin-top:2rem;}");
        sb.Append(".inc-sub{font-size:0.9rem;margin:0.35rem 0 0;}");
        sb.Append(".inc-list{margin:0.5rem 0;padding-left:1.25rem;} .inc-list li{margin:0.2rem 0;list-style-type:square;}");
        sb.Append(".exc-bar{background:").Append(primary).Append(";color:#fff;padding:0.5rem 0.75rem;font-weight:700;}");
        sb.Append(".exc-box{background:#e5e7eb;padding:0.75rem 1rem;border-radius:6px;margin-top:0;}");
        sb.Append(".exc-box .inc-list{margin:0.25rem 0;}");
        sb.Append(".agency-grid{margin-top:0.5rem;border:1px solid #d1d5db;border-radius:6px;overflow:hidden;}");
        sb.Append(".agency-row{display:flex;}");
        sb.Append(".agency-cell{flex:1;padding:0.45rem 0.65rem;font-size:0.9rem;border-bottom:1px solid #e5e7eb;}");
        sb.Append(".agency-row:last-child .agency-cell{border-bottom:none;}");
        sb.Append(".agency-row.info-header .agency-cell{background:").Append(primary).Append(";color:#fff;font-weight:600;}");
        sb.Append(".agency-row:not(.info-header) .agency-cell{background:#e5e7eb;}");
        sb.Append(".bank-grid{margin-top:0.5rem;border:1px solid #d1d5db;border-radius:6px;overflow:hidden;}");
        sb.Append(".bank-row{display:flex;}");
        sb.Append(".bank-cell{flex:1;padding:0.45rem 0.65rem;font-size:0.9rem;border-bottom:1px solid #e5e7eb;}");
        sb.Append(".bank-row:last-child .bank-cell{border-bottom:none;}");
        sb.Append(".bank-row.info-header .bank-cell{background:").Append(primary).Append(";color:#fff;font-weight:600;}");
        sb.Append(".bank-row:not(.info-header) .bank-cell{background:#e5e7eb;}");
        sb.Append(".qr-section{display:flex;flex-wrap:wrap;gap:1rem;margin-top:0.75rem;}");
        sb.Append(".qr-item{text-align:center;}");
        sb.Append(".qr-item img{width:120px;height:120px;object-fit:contain;}");
        sb.Append(".qr-item .qr-label{font-size:0.9rem;font-weight:600;margin-top:0.35rem;}");
        sb.Append("@media print{.acc-card,.day-block{page-break-inside:avoid;}}");
        sb.Append("</style></head><body>");

        // —— Page 1: Cover block + Details and Pricing in one row ——
        var showClientPhone = !string.IsNullOrEmpty(m.ClientPhone) && !IsAllZeros(m.ClientPhone);
        sb.Append("<div class=\"page-1\">");
        sb.Append("<div class=\"cover-block\">");
        if (!string.IsNullOrWhiteSpace(m.AgencyLogoUrl))
        {
            var logoSrc = m.AgencyLogoUrl.IndexOf('"') >= 0 ? m.AgencyLogoUrl.Replace("\"", "&quot;") : m.AgencyLogoUrl;
            sb.Append("<p class=\"cover-logo\"><img src=\"").Append(logoSrc).Append("\" alt=\"\" /></p>");
        }
        sb.Append("<p class=\"cover-title\">").Append(H(coverTitle)).Append("</p>");
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
        sb.Append("<div class=\"info-row\"><div class=\"info-tab\"></div><div class=\"info-cell\">Number of Extra beds</div><div class=\"info-cell val\">").Append(m.TotalExtraBeds.ToString()).Append("</div><div class=\"info-tab-r\"></div></div>");
        sb.Append("<div class=\"info-row\"><div class=\"info-tab\"></div><div class=\"info-cell\">Number of CNB</div><div class=\"info-cell val\">").Append(m.TotalCnbCount.ToString()).Append("</div><div class=\"info-tab-r\"></div></div>");
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
            sb.Append("<p class=\"day-para\">").Append(H(d.Description)).Append("</p>");
            if (d.ExtraBedCount > 0 || d.CnbCount > 0)
            {
                var parts = new List<string>();
                if (d.ExtraBedCount > 0) parts.Add("Extra beds: " + d.ExtraBedCount);
                if (d.CnbCount > 0) parts.Add("CNB: " + d.CnbCount);
                sb.Append("<p class=\"day-extra\">").Append(H(string.Join(" | ", parts))).Append("</p>");
            }
            sb.Append("</div>");
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

        // —— Bank Details (tenant can hide via PdfShowBankDetails) ——
        var bankAccounts = (m.BankAccounts ?? []).ToList();
        if (showBank && bankAccounts.Count > 0)
        {
            sb.Append("<h2 class=\"section-head\">Bank Details</h2>");
            sb.Append("<div class=\"bank-grid\">");
            sb.Append("<div class=\"bank-row info-header\"><div class=\"bank-cell\">Account Holder</div><div class=\"bank-cell\">Bank Name</div><div class=\"bank-cell\">Account Number</div><div class=\"bank-cell\">IFSC</div><div class=\"bank-cell\">Branch</div></div>");
            foreach (var b in bankAccounts)
            {
                sb.Append("<div class=\"bank-row\"><div class=\"bank-cell\">").Append(H(b.AccountHolderName)).Append("</div><div class=\"bank-cell\">").Append(H(b.BankName)).Append("</div><div class=\"bank-cell\">").Append(H(b.AccountNumber)).Append("</div><div class=\"bank-cell\">").Append(H(b.IFSC)).Append("</div><div class=\"bank-cell\">").Append(H(b.Branch ?? "–")).Append("</div></div>");
            }
            sb.Append("</div>");
        }

        // —— Payment QR Codes (tenant can hide via PdfShowQrCodes) ——
        var qrCodes = (m.QrCodes ?? []).ToList();
        if (showQr && qrCodes.Count > 0)
        {
            sb.Append("<h2 class=\"section-head\">Payment (Scan QR)</h2>");
            sb.Append("<div class=\"qr-section\">");
            foreach (var q in qrCodes)
            {
                if (string.IsNullOrEmpty(q.ImageUrl)) continue;
                var safeSrc = q.ImageUrl.IndexOf('"') >= 0 ? q.ImageUrl.Replace("\"", "&quot;") : q.ImageUrl;
                sb.Append("<div class=\"qr-item\"><img src=\"").Append(safeSrc).Append("\" alt=\"\"/><div class=\"qr-label\">").Append(H(q.Label)).Append("</div></div>");
            }
            sb.Append("</div>");
        }

        sb.Append("</body></html>");
        return sb.ToString();
    }

    private static string BuildHtmlV2(PackagePdfModel m)
    {
        var primary = SanitizeCssColor(m.PrimaryColor) ?? "#c026d3";
        var secondary = SanitizeCssColor(m.SecondaryColor) ?? "#ec4899";
        var coverTitle = string.IsNullOrWhiteSpace(m.CoverTitle) ? "Holiday Quote" : m.CoverTitle.Trim();
        var showBank = m.ShowBankDetails ?? true;
        var showQr = m.ShowQrCodes ?? true;
        var showClientPhone = !string.IsNullOrEmpty(m.ClientPhone) && !IsAllZeros(m.ClientPhone);

        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"UTF-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        sb.Append("<style>");
        sb.Append("*{box-sizing:border-box;} body{margin:0;font-family:Arial,Helvetica,sans-serif;font-size:10pt;line-height:1.5;color:#0f172a;}");
        sb.Append(".watermark{position:fixed;top:0;left:0;right:0;bottom:0;z-index:-1;opacity:.06;pointer-events:none;overflow:hidden;}");
        sb.Append(".watermark div{font-size:22px;line-height:2.1;color:").Append(primary).Append(";white-space:nowrap;transform:rotate(-19deg) translate(-120px,-40px);transform-origin:0 0;}");
        sb.Append(".section{margin-bottom:18px;}");
        sb.Append(".hero{padding:12px 14px;border:1px solid #d1d5db;border-radius:8px;background:#fff;}");
        sb.Append(".hero-top{display:flex;justify-content:space-between;align-items:center;gap:12px;}");
        sb.Append(".logo img{max-height:52px;max-width:190px;object-fit:contain;}");
        sb.Append(".trip-id{font-weight:700;color:").Append(primary).Append(";font-size:11pt;}");
        sb.Append(".hero h1{margin:8px 0 2px;font-size:16pt;color:#111827;letter-spacing:.01em;}");
        sb.Append(".hero .meta{font-size:9.5pt;color:#334155;margin:3px 0;}");
        sb.Append(".consultant{margin-top:10px;padding-top:8px;border-top:1px dashed #cbd5e1;font-size:9pt;color:#475569;}");
        sb.Append(".title-bar{margin:16px 0 9px;background:#111827;color:#fff;padding:7px 10px;border-radius:4px;font-weight:700;letter-spacing:.03em;text-transform:uppercase;font-size:9pt;}");
        sb.Append(".grid-2{display:grid;grid-template-columns:1fr 1fr;gap:12px;}");
        sb.Append(".card{border:1px solid #dbe1ea;border-radius:10px;padding:11px;background:#fff;}");
        sb.Append(".kpi{font-size:8.3pt;color:#64748b;text-transform:uppercase;letter-spacing:.08em;}");
        sb.Append(".val{font-size:12.5pt;font-weight:700;color:#111827;margin-top:3px;}");
        sb.Append(".quote{border:1px solid #d1d5db;border-radius:8px;padding:12px;background:#fff;text-align:center;}");
        sb.Append(".quote .amt{font-size:17pt;font-weight:800;color:#111827;margin:6px 0 2px;}");
        sb.Append(".quote .sub{font-size:8.5pt;color:#6b7280;}");
        sb.Append(".acc-item{border:1px solid #e2e8f0;border-radius:10px;padding:11px;margin-bottom:10px;page-break-inside:avoid;background:#fff;}");
        sb.Append(".acc-head{display:flex;justify-content:space-between;align-items:center;gap:8px;margin-bottom:6px;}");
        sb.Append(".acc-name{font-size:11pt;font-weight:700;color:#1f2937;}");
        sb.Append(".pill{font-size:8pt;background:#f3f4f6;color:#111827;border:1px solid #d1d5db;padding:2px 8px;border-radius:999px;}");
        sb.Append(".acc-meta{font-size:9pt;color:#475569;margin-bottom:6px;}");
        sb.Append(".acc-divider{height:1px;background:#f1f5f9;margin:7px 0;}");
        sb.Append(".acc-imgs{display:grid;grid-template-columns:1fr 1fr;gap:8px;margin-top:6px;}");
        sb.Append(".acc-imgs img{width:100%;height:140px;object-fit:cover;border-radius:8px;border:1px solid #e5e7eb;}");
        sb.Append(".day-block{border:1px solid #e2e8f0;border-radius:10px;margin-bottom:10px;page-break-inside:avoid;background:#fff;}");
        sb.Append(".day-head{display:flex;align-items:center;gap:10px;padding:8px 10px;background:#f3f4f6;color:#111827;border-radius:10px 10px 0 0;border-bottom:1px solid #e5e7eb;}");
        sb.Append(".day-no{display:inline-flex;width:26px;height:26px;border-radius:999px;background:#111827;color:#fff;align-items:center;justify-content:center;font-weight:700;font-size:9pt;}");
        sb.Append(".day-title{font-weight:700;font-size:10pt;}");
        sb.Append(".day-desc{padding:11px 12px;white-space:pre-wrap;line-height:1.6;color:#1f2937;}");
        sb.Append(".split{display:grid;grid-template-columns:1fr 1fr;gap:12px;}");
        sb.Append(".list-box{border:1px solid #e2e8f0;border-radius:10px;padding:10px;background:#fff;}");
        sb.Append(".list-box h3{margin:0 0 8px;font-size:10.5pt;color:#111827;}");
        sb.Append(".list-box ul{margin:0;padding-left:18px;} .list-box li{margin:3px 0;}");
        sb.Append(".terms{border:1px solid #e2e8f0;border-radius:10px;padding:10px;background:#fff;}");
        sb.Append(".terms h3{margin:0 0 8px;font-size:10.5pt;color:#111827;}");
        sb.Append(".terms ul{margin:0;padding-left:18px;} .terms li{margin:4px 0;}");
        sb.Append(".table{width:100%;border-collapse:collapse;font-size:9pt;background:#fff;border:1px solid #e2e8f0;border-radius:10px;overflow:hidden;}");
        sb.Append(".table th,.table td{border-bottom:1px solid #e5e7eb;padding:8px 8px;text-align:left;vertical-align:top;}");
        sb.Append(".table th{background:").Append(primary).Append(";color:#fff;font-weight:700;}");
        sb.Append(".table tr:last-child td{border-bottom:none;}");
        sb.Append(".contact{margin-top:10px;padding:10px;border:1px dashed #cbd5e1;border-radius:10px;background:#f8fafc;font-size:9pt;}");
        sb.Append(".contact b{color:#111827;}");
        sb.Append(".qr-wrap{display:flex;gap:10px;flex-wrap:wrap;margin-top:8px;}");
        sb.Append(".qr{border:1px solid #e5e7eb;border-radius:8px;padding:6px;text-align:center;background:#fff;}");
        sb.Append(".qr img{width:92px;height:92px;object-fit:contain;display:block;}");
        sb.Append(".qr span{display:block;font-size:8pt;color:#475569;margin-top:4px;}");
        sb.Append(".page-break{page-break-before:always;}");
        sb.Append(".pdf-footer{margin-top:12px;padding-top:8px;border-top:1px dashed #cbd5e1;font-size:8pt;color:#64748b;display:flex;justify-content:space-between;}");
        sb.Append("@media print{.day-block,.acc-item{page-break-inside:avoid;}}");
        sb.Append("</style></head><body>");

        sb.Append("<div class=\"watermark\"><div>");
        var markText = string.IsNullOrWhiteSpace(m.AgencyName) ? "Travel Pathways" : m.AgencyName.Trim();
        for (var i = 0; i < 24; i++) sb.Append(H(markText)).Append(" • ").Append(H(m.PackageName)).Append(" • ");
        sb.Append("</div></div>");

        sb.Append("<section class=\"section hero\">");
        sb.Append("<div class=\"hero-top\"><div class=\"logo\">");
        if (!string.IsNullOrWhiteSpace(m.AgencyLogoUrl))
        {
            var logoSrc = m.AgencyLogoUrl.IndexOf('"') >= 0 ? m.AgencyLogoUrl.Replace("\"", "&quot;") : m.AgencyLogoUrl;
            sb.Append("<img src=\"").Append(logoSrc).Append("\" alt=\"\" />");
        }
        sb.Append("</div><div class=\"trip-id\">Trip ID: ").Append(H(m.PackageName)).Append("</div></div>");
        sb.Append("<h1>").Append(H(coverTitle)).Append("</h1>");
        sb.Append("<p class=\"meta\"><b>").Append(H(m.ClientName)).Append("</b>");
        if (showClientPhone) sb.Append(" • ").Append(H(m.ClientPhone));
        sb.Append("</p>");
        sb.Append("<p class=\"meta\">Starts: ").Append(H(m.StartDate)).Append(" Ends: ").Append(H(m.EndDate)).Append(" (").Append(H(m.DaysLabel)).Append(")</p>");
        sb.Append("<p class=\"meta\">Dear ").Append(H(m.ClientName)).Append(", greetings from ").Append(H(m.AgencyName ?? "our team")).Append(".</p>");
        sb.Append("<div class=\"consultant\">");
        if (!string.IsNullOrWhiteSpace(m.AgencyName)) sb.Append("<div>Your Holiday Consultant: <b>").Append(H(m.AgencyName)).Append("</b></div>");
        if (!string.IsNullOrWhiteSpace(m.AgencyPhone)) sb.Append("<div>Contact: ").Append(H(m.AgencyPhone)).Append("</div>");
        sb.Append("</div></section>");

        sb.Append("<div class=\"title-bar\">Quote Summary</div>");
        sb.Append("<section class=\"grid-2 section\">");
        sb.Append("<div class=\"card\">");
        sb.Append("<div class=\"kpi\">Destination</div><div class=\"val\">").Append(H(m.DropLocation ?? m.PickUpLocation ?? "Jammu & Kashmir")).Append("</div>");
        sb.Append("<div class=\"kpi\" style=\"margin-top:8px;\">Start Date</div><div class=\"val\">").Append(H(m.StartDate)).Append("</div>");
        sb.Append("<div class=\"kpi\" style=\"margin-top:8px;\">Duration</div><div class=\"val\">").Append(H(m.DaysLabel)).Append("</div>");
        sb.Append("<div class=\"kpi\" style=\"margin-top:8px;\">Pax</div><div class=\"val\">").Append(H($"{m.NumberOfAdults} Adults{(m.NumberOfChildren > 0 ? $" + {m.NumberOfChildren} Children" : "")}")).Append("</div>");
        sb.Append("</div>");
        sb.Append("<div class=\"quote\"><div class=\"kpi\">Quote Price</div><div class=\"amt\">").Append(H(m.FinalAmount)).Append("</div><div class=\"sub\">Total (INR) • excluding GST</div></div>");
        sb.Append("</section>");

        sb.Append("<div class=\"title-bar\">Hotels / Accommodations</div>");
        foreach (var h in m.Hotels ?? [])
        {
            sb.Append("<div class=\"acc-item\">");
            sb.Append("<div class=\"acc-head\"><div class=\"acc-name\">").Append(H(h.Name)).Append("</div><div class=\"pill\">").Append(h.IsHouseboat ? "Houseboat" : "Hotel").Append("</div></div>");
            sb.Append("<div class=\"acc-meta\">").Append(H(h.Location));
            if (h.StarRating > 0) sb.Append(" • ").Append(RenderStars(h.StarRating));
            sb.Append(" • ").Append(H(h.MealPlan)).Append(" • ").Append(h.Nights).Append(" Night(s)</div>");
            sb.Append("<div class=\"acc-divider\"></div>");
            sb.Append("<div class=\"acc-meta\">Rooms: ").Append(m.FirstDayRooms).Append(" • Extra Bed: ").Append(m.TotalExtraBeds).Append(" • CNB: ").Append(m.TotalCnbCount).Append("</div>");
            if (h.ImageUrls?.Count > 0)
            {
                sb.Append("<div class=\"acc-imgs\">");
                for (var i = 0; i < Math.Min(2, h.ImageUrls.Count); i++)
                {
                    var url = h.ImageUrls[i];
                    if (string.IsNullOrWhiteSpace(url)) continue;
                    var safeSrc = url.IndexOf('"') >= 0 ? url.Replace("\"", "&quot;") : url;
                    sb.Append("<img src=\"").Append(safeSrc).Append("\" alt=\"\"/>");
                }
                sb.Append("</div>");
            }
            sb.Append("</div>");
        }

        sb.Append("<div class=\"title-bar\">Transportation</div>");
        sb.Append("<div class=\"card section\">Transportation route: ").Append(H(m.PickUpLocation)).Append(" to ").Append(H(m.DropLocation)).Append("</div>");

        sb.Append("<div class=\"page-break\"></div>");
        sb.Append("<div class=\"title-bar\">Day Wise Itinerary</div>");
        foreach (var d in m.Days ?? [])
        {
            sb.Append("<div class=\"day-block\">");
            sb.Append("<div class=\"day-head\"><span class=\"day-no\">").Append(d.DayNumber).Append("</span><span class=\"day-title\">").Append(d.DayNumber).Append(" Day • ").Append(H(d.Title)).Append("</span></div>");
            sb.Append("<div class=\"day-desc\">").Append(H(d.Description)).Append("</div>");
            sb.Append("</div>");
        }

        sb.Append("<div class=\"title-bar\">Inclusions / Exclusions</div>");
        sb.Append("<section class=\"split section\">");
        sb.Append("<div class=\"list-box\"><h3>Inclusions</h3><ul>");
        foreach (var label in m.InclusionLabels ?? []) sb.Append("<li>").Append(H(label)).Append("</li>");
        if ((m.InclusionLabels?.Count ?? 0) == 0) sb.Append("<li>As per package details</li>");
        sb.Append("</ul></div>");
        sb.Append("<div class=\"list-box\"><h3>Exclusions</h3><ul>");
        foreach (var label in m.ExclusionLabels ?? []) sb.Append("<li>").Append(H(label)).Append("</li>");
        if ((m.ExclusionLabels?.Count ?? 0) == 0) sb.Append("<li>Items not listed under inclusions</li>");
        sb.Append("</ul></div>");
        sb.Append("</section>");

        sb.Append("<div class=\"title-bar\">Terms & Conditions</div>");
        sb.Append("<div class=\"terms section\"><h3>General Terms</h3><ul>");
        sb.Append("<li>Rates are subject to availability at the time of confirmation.</li>");
        sb.Append("<li>Any increase in transport, taxes, or supplier charges will be applicable.</li>");
        sb.Append("<li>Guest must carry valid government photo ID at check-in.</li>");
        sb.Append("<li>Anything not mentioned in inclusions is treated as excluded.</li>");
        sb.Append("<li>Cancellation charges apply as per the booking policy.</li>");
        sb.Append("<li>Supplement costs (if any) are payable directly at destination.</li>");
        sb.Append("</ul></div>");

        if (showBank && (m.BankAccounts?.Count ?? 0) > 0)
        {
            sb.Append("<div class=\"title-bar\">Bank Details</div>");
            sb.Append("<table class=\"table section\"><thead><tr><th>Account Holder</th><th>Bank Name</th><th>Account Number</th><th>IFSC</th></tr></thead><tbody>");
            foreach (var b in m.BankAccounts ?? [])
            {
                sb.Append("<tr><td>").Append(H(b.AccountHolderName)).Append("</td><td>").Append(H(b.BankName)).Append("</td><td>").Append(H(b.AccountNumber)).Append("</td><td>").Append(H(b.IFSC)).Append("</td></tr>");
            }
            sb.Append("</tbody></table>");
        }

        if (showQr && (m.QrCodes?.Count ?? 0) > 0)
        {
            sb.Append("<div class=\"title-bar\">Scan & Pay</div><div class=\"qr-wrap section\">");
            foreach (var q in m.QrCodes ?? [])
            {
                if (string.IsNullOrWhiteSpace(q.ImageUrl)) continue;
                var safeSrc = q.ImageUrl.IndexOf('"') >= 0 ? q.ImageUrl.Replace("\"", "&quot;") : q.ImageUrl;
                sb.Append("<div class=\"qr\"><img src=\"").Append(safeSrc).Append("\" alt=\"\"/><span>").Append(H(q.Label)).Append("</span></div>");
            }
            sb.Append("</div>");
        }

        sb.Append("<div class=\"contact\"><div><b>").Append(H(m.AgencyName ?? "Travel Agency")).Append("</b></div>");
        if (!string.IsNullOrWhiteSpace(m.AgencyPhone)) sb.Append("<div>Phone: ").Append(H(m.AgencyPhone)).Append("</div>");
        if (!string.IsNullOrWhiteSpace(m.AgencyEmail)) sb.Append("<div>Email: ").Append(H(m.AgencyEmail)).Append("</div>");
        if (!string.IsNullOrWhiteSpace(m.ManagingDirectorName)) sb.Append("<div>Contact Person: ").Append(H(m.ManagingDirectorName)).Append("</div>");
        sb.Append("</div>");

        sb.Append("<div class=\"pdf-footer\"><span>").Append(H(m.AgencyName ?? "Travel Agency")).Append("</span><span>Generated on ").Append(H(m.GeneratedDate)).Append("</span></div>");
        sb.Append("</body></html>");
        return sb.ToString();
    }

    private static string BuildCustomHtml(PackagePdfModel m)
    {
        var template = m.CustomHtmlTemplate ?? "";
        if (string.IsNullOrWhiteSpace(template))
            return BuildHtmlV2(m);

        string hotelsHtml = string.Join("", (m.Hotels ?? []).Select(h =>
            $"<div><strong>{H(h.Name)}</strong> - {H(h.Location)} - {h.Nights} night(s)</div>"));
        string daysHtml = string.Join("", (m.Days ?? []).Select(d =>
            $"<div><strong>Day {d.DayNumber}: {H(d.Title)}</strong><div>{H(d.Description)}</div></div>"));
        string inclusionsHtml = string.Join("", (m.InclusionLabels ?? []).Select(x => $"<li>{H(x)}</li>"));
        string exclusionsHtml = string.Join("", (m.ExclusionLabels ?? []).Select(x => $"<li>{H(x)}</li>"));
        string bankHtml = string.Join("", (m.BankAccounts ?? []).Select(b =>
            $"<tr><td>{H(b.AccountHolderName)}</td><td>{H(b.BankName)}</td><td>{H(b.AccountNumber)}</td><td>{H(b.IFSC)}</td></tr>"));
        string qrHtml = string.Join("", (m.QrCodes ?? []).Where(q => !string.IsNullOrWhiteSpace(q.ImageUrl)).Select(q =>
        {
            var safeSrc = q.ImageUrl.IndexOf('"') >= 0 ? q.ImageUrl.Replace("\"", "&quot;") : q.ImageUrl;
            return $"<div style=\"display:inline-block;text-align:center;margin-right:8px;\"><img src=\"{safeSrc}\" style=\"width:90px;height:90px;object-fit:contain;\" alt=\"\" /><div>{H(q.Label)}</div></div>";
        }));

        var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["{{PackageName}}"] = H(m.PackageName),
            ["{{ClientName}}"] = H(m.ClientName),
            ["{{ClientPhone}}"] = H(m.ClientPhone),
            ["{{ClientEmail}}"] = H(m.ClientEmail),
            ["{{StartDate}}"] = H(m.StartDate),
            ["{{EndDate}}"] = H(m.EndDate),
            ["{{DaysLabel}}"] = H(m.DaysLabel),
            ["{{PickUpLocation}}"] = H(m.PickUpLocation),
            ["{{DropLocation}}"] = H(m.DropLocation),
            ["{{FinalAmount}}"] = H(m.FinalAmount),
            ["{{TotalAmount}}"] = H(m.TotalAmount),
            ["{{AdvanceAmount}}"] = H(m.AdvanceAmount),
            ["{{BalanceAmount}}"] = H(m.BalanceAmount),
            ["{{AgencyName}}"] = H(m.AgencyName),
            ["{{AgencyPhone}}"] = H(m.AgencyPhone),
            ["{{AgencyEmail}}"] = H(m.AgencyEmail),
            ["{{GeneratedDate}}"] = H(m.GeneratedDate),
            ["{{HotelsHtml}}"] = hotelsHtml,
            ["{{DaysHtml}}"] = daysHtml,
            ["{{InclusionsHtml}}"] = inclusionsHtml,
            ["{{ExclusionsHtml}}"] = exclusionsHtml,
            ["{{BankRowsHtml}}"] = bankHtml,
            ["{{QrHtml}}"] = qrHtml
        };

        foreach (var kv in replacements)
            template = template.Replace(kv.Key, kv.Value, StringComparison.OrdinalIgnoreCase);

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

    private static bool IsAllZeros(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return true;
        return s.Trim().All(c => c == '0' || char.IsWhiteSpace(c));
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
