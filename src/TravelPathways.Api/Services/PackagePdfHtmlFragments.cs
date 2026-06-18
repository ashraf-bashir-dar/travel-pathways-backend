using System.Net;
using System.Text;
using TravelPathways.Api.Data.Entities;
using TravelPathways.Api.Localization;

namespace TravelPathways.Api.Services;

/// <summary>Shared HTML fragments for package PDF rendering.</summary>
public static class PackagePdfHtmlFragments
{
    public static string? FormatVehicleName(Vehicle? vehicle)
    {
        if (vehicle is null) return null;
        var type = vehicle.VehicleType.ToString();
        var model = vehicle.VehicleModel?.Trim();
        return string.IsNullOrEmpty(model) ? type : $"{type} - {model}";
    }

    public static string? FormatTransportRoute(string? pickup, string? drop)
    {
        var from = pickup?.Trim();
        var to = drop?.Trim();
        if (string.IsNullOrEmpty(from) && string.IsNullOrEmpty(to)) return null;
        if (string.IsNullOrEmpty(from)) return to;
        if (string.IsNullOrEmpty(to)) return from;
        return $"{from} → {to}";
    }

    public static string BuildTransportSectionHtml(PackagePdfModel model)
    {
        var hasVehicle = !string.IsNullOrWhiteSpace(model.VehicleName);
        var hasRoute = !string.IsNullOrWhiteSpace(model.TransportRoute);
        if (!hasVehicle && !hasRoute) return "";

        var labels = model.Labels ?? PdfLocalizedStrings.English();
        var sb = new StringBuilder();
        sb.Append("<div class=\"fact-card transport-section\"><h3>")
            .Append(WebUtility.HtmlEncode(labels.Transport))
            .Append("</h3><div class=\"fact-rows\">");
        if (hasVehicle)
        {
            sb.Append("<div class=\"row\"><span>")
                .Append(WebUtility.HtmlEncode(labels.Vehicle))
                .Append("</span><span>")
                .Append(WebUtility.HtmlEncode(model.VehicleName!.Trim()))
                .Append("</span></div>");
        }
        if (hasRoute)
        {
            sb.Append("<div class=\"row\"><span>")
                .Append(WebUtility.HtmlEncode(labels.Route))
                .Append("</span><span>")
                .Append(WebUtility.HtmlEncode(model.TransportRoute!.Trim()))
                .Append("</span></div>");
        }
        sb.Append("</div></div>");
        return sb.ToString();
    }
}
