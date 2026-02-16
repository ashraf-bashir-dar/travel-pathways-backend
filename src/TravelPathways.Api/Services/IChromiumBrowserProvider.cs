using PuppeteerSharp;

namespace TravelPathways.Api.Services;

/// <summary>Provides a shared Chromium browser instance for PDF generation. Avoids launching a new browser per request.</summary>
public interface IChromiumBrowserProvider
{
    /// <summary>Runs the given action with a new page from the shared browser. The page is closed after the action completes.</summary>
    Task<T> RunWithPageAsync<T>(Func<IPage, Task<T>> action, CancellationToken cancellationToken = default);

    /// <summary>Launches the browser in the background so the first PDF request does not wait. Call on app startup.</summary>
    Task WarmUpAsync(CancellationToken cancellationToken = default);
}
