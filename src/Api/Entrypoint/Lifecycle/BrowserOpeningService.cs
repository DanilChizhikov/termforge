using System.ComponentModel;
using Microsoft.Extensions.Options;

namespace DTech.Entrypoint.Lifecycle;

internal sealed class BrowserOpeningService : IHostedService
{
    private readonly BootstrapUrlFactory _bootstrapUrlFactory;
    private readonly IBrowserLauncher _browserLauncher;
    private readonly IOptions<LifecycleOptions> _options;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<BrowserOpeningService> _logger;

    public BrowserOpeningService(
        BootstrapUrlFactory bootstrapUrlFactory,
        IBrowserLauncher browserLauncher,
        IOptions<LifecycleOptions> options,
        IHostApplicationLifetime lifetime,
        ILogger<BrowserOpeningService> logger)
    {
        _bootstrapUrlFactory = bootstrapUrlFactory;
        _browserLauncher = browserLauncher;
        _options = options;
        _lifetime = lifetime;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Value.OpenBrowser)
        {
            _logger.LogInformation("Opening the browser is disabled by configuration");
            return Task.CompletedTask;
        }

        _lifetime.ApplicationStarted.Register(OpenBrowser);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    internal void OpenBrowser()
    {
        if (_lifetime.ApplicationStopping.IsCancellationRequested)
        {
            return;
        }

        var bootstrapUrl = _bootstrapUrlFactory.Create();
        if (bootstrapUrl is null)
        {
            _logger.LogWarning("The browser was not opened: the server has no loopback HTTP address");
            return;
        }

        try
        {
            _browserLauncher.Open(bootstrapUrl);
            var origin = bootstrapUrl.GetLeftPart(UriPartial.Authority);
            _logger.LogInformation($"Opened the browser at {origin}");
        }
        catch (Win32Exception exception)
        {
            _logger.LogError($"The browser could not be opened, native error code {exception.NativeErrorCode}");
        }
        catch (InvalidOperationException)
        {
            _logger.LogError("The browser could not be opened: no browser command is available");
        }
    }
}
