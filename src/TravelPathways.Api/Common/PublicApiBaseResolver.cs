using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace TravelPathways.Api.Common;

/// <summary>
/// Single place for computing the URL prefix browsers should use for <c>/uploads</c> and other API-hosted assets.
/// Uses <see cref="IConfiguration"/> first, then the current request (after forwarded headers) as fallback.
/// </summary>
public static class PublicApiBaseResolver
{
    public static string? Resolve(IConfiguration configuration, HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(httpContext);
        return Resolve(configuration, httpContext.Request);
    }

    public static string? Resolve(IConfiguration configuration, HttpRequest? request)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        static string? Trim(string? s) =>
            string.IsNullOrWhiteSpace(s) ? null : s.Trim().TrimEnd('/');

        var configured = Trim(configuration["Api:PublicBaseUrl"] ?? configuration["Api__PublicBaseUrl"]);
        if (!string.IsNullOrEmpty(configured))
            return configured;

        configured = Trim(
            configuration["PdfGenerator:BaseUrl"] ??
            configuration["PdfGenerator__BaseUrl"] ??
            configuration["Api:BaseUrl"] ??
            configuration["Api__BaseUrl"]);
        if (!string.IsNullOrEmpty(configured))
            return configured;

        if (request is not null && !string.IsNullOrEmpty(request.Host.Value))
        {
            try
            {
                return $"{request.Scheme}://{request.Host.Value}".TrimEnd('/');
            }
            catch
            {
                /* ignore */
            }
        }

        return null;
    }
}
