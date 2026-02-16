namespace TravelPathways.Api.Services;

/// <summary>Warms up Chromium on startup and disposes it when the application shuts down.</summary>
public sealed class ChromiumBrowserHostedService : IHostedService
{
    private readonly IChromiumBrowserProvider _provider;
    private readonly ILogger<ChromiumBrowserHostedService> _logger;

    public ChromiumBrowserHostedService(IChromiumBrowserProvider provider, ILogger<ChromiumBrowserHostedService> logger)
    {
        _provider = provider;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Warm up browser in background so first PDF request doesn't wait 3–8 seconds for Chromium launch
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
                await _provider.WarmUpAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("PDF browser warm-up completed.");
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PDF browser warm-up failed; first PDF request may be slow.");
            }
        }, cancellationToken);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_provider is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
    }
}
