using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace TravelPathways.Api.Services;

/// <summary>Shared Headless Chrome HTML → PDF rendering tuned for server-side generation (no external network).</summary>
public static class PdfChromiumRenderer
{
    /// <summary>
    /// Renders HTML to PDF. Blocks HTTP(S) subresource requests so Chromium cannot hang on slow/missing
    /// upload URLs — images must be inlined as data: URLs before calling this.
    /// </summary>
    public static async Task<byte[]> RenderHtmlToPdfAsync(
        IPage page,
        string html,
        PdfOptions pdfOptions,
        int timeoutMs,
        CancellationToken cancellationToken = default)
    {
        page.DefaultTimeout = timeoutMs;
        page.DefaultNavigationTimeout = timeoutMs;

        await page.SetRequestInterceptionAsync(true).ConfigureAwait(false);
        page.Request += async (_, e) =>
        {
            var url = e.Request.Url;
            if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("about:", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("blob:", StringComparison.OrdinalIgnoreCase))
            {
                await e.Request.ContinueAsync().ConfigureAwait(false);
            }
            else
            {
                await e.Request.AbortAsync().ConfigureAwait(false);
            }
        };

        await page.SetJavaScriptEnabledAsync(false).ConfigureAwait(false);
        await page.SetContentAsync(
            html,
            new NavigationOptions
            {
                WaitUntil = [WaitUntilNavigation.DOMContentLoaded],
                Timeout = timeoutMs
            }).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
        return await page.PdfDataAsync(pdfOptions).ConfigureAwait(false);
    }
}
