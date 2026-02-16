using PuppeteerSharp;

namespace TravelPathways.Api.Services;

/// <summary>Lazy-initialized shared Chromium browser for PDF generation. Reuses one browser process instead of launching per request.</summary>
public sealed class ChromiumBrowserProvider : IChromiumBrowserProvider, IAsyncDisposable
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChromiumBrowserProvider> _logger;
    private IBrowser? _browser;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public ChromiumBrowserProvider(IConfiguration configuration, ILogger<ChromiumBrowserProvider> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task WarmUpAsync(CancellationToken cancellationToken = default) => await GetOrLaunchBrowserAsync(cancellationToken).ConfigureAwait(false);

    public async Task<T> RunWithPageAsync<T>(Func<IPage, Task<T>> action, CancellationToken cancellationToken = default)
    {
        var browser = await GetOrLaunchBrowserAsync(cancellationToken).ConfigureAwait(false);
        IPage? page = null;
        try
        {
            page = await browser.NewPageAsync().ConfigureAwait(false);
            return await action(page).ConfigureAwait(false);
        }
        finally
        {
            if (page != null)
            {
                try { await page.CloseAsync().ConfigureAwait(false); } catch { /* best effort */ }
            }
        }
    }

    private async Task<IBrowser> GetOrLaunchBrowserAsync(CancellationToken cancellationToken)
    {
        if (_browser != null && _browser.IsConnected)
            return _browser;

        await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_browser != null && _browser.IsConnected)
                return _browser;

            if (_browser != null)
            {
                try { await _browser.DisposeAsync().ConfigureAwait(false); } catch { }
                _browser = null;
            }

            _browser = await LaunchBrowserAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Chromium browser launched for PDF generation (shared instance).");
            return _browser;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task<IBrowser> LaunchBrowserAsync(CancellationToken cancellationToken)
    {
        var chromePath = _configuration["PdfGenerator:ChromeExecutablePath"]?.Trim()
            ?? _configuration["PDF__ChromeExecutablePath"]?.Trim();

        var launchOptions = new LaunchOptions
        {
            Headless = true,
            Args = new[]
            {
                "--no-sandbox",
                "--disable-setuid-sandbox",
                "--disable-dev-shm-usage",
                "--disable-gpu",
                "--disable-software-rasterizer",
                "--disable-extensions",
                "--no-first-run",
                "--no-zygote",
                "--disable-background-networking",
                "--disable-default-apps",
                "--disable-sync",
                "--metrics-recording-only",
                "--mute-audio",
                "--no-default-browser-check"
            }
        };

        if (!string.IsNullOrEmpty(chromePath))
        {
            launchOptions.ExecutablePath = chromePath;
        }
        else
        {
            var browserFetcher = new BrowserFetcher();
            await browserFetcher.DownloadAsync().ConfigureAwait(false);
        }

        try
        {
            return await Puppeteer.LaunchAsync(launchOptions).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var msg = ex.Message;
            if (msg.Contains("side-by-side", StringComparison.OrdinalIgnoreCase) || msg.Contains("failed to start", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "PDF generation failed: Chromium could not start. On Azure App Service (Windows) this usually means missing runtimes. " +
                    "Use Azure App Service on Linux, or deploy with a Docker image that includes Chromium (see README or deploy Dockerfile from the API project). " +
                    "You can also set PdfGenerator:ChromeExecutablePath to a Chromium path if you install it yourself. Original error: " + msg, ex);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser == null) return;
        await _initLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_browser != null)
            {
                try { await _browser.DisposeAsync().ConfigureAwait(false); } catch { }
                _browser = null;
                _logger.LogInformation("Chromium browser disposed.");
            }
        }
        finally
        {
            _initLock.Release();
        }
    }
}
