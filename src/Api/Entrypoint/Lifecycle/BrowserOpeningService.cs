using System.ComponentModel;
using Microsoft.Extensions.Options;

namespace DTech.Entrypoint.Lifecycle;

internal sealed class BrowserOpeningService : IHostedService
{
    private readonly BootstrapUrlFactory _bootstrapUrlFactory;
    private readonly IBrowserLauncher _browserLauncher;
    private readonly BootstrapRedirectFile _redirectFile;
    private readonly IOptions<LifecycleOptions> _options;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<BrowserOpeningService> _logger;

    public BrowserOpeningService(
        BootstrapUrlFactory bootstrapUrlFactory,
        IBrowserLauncher browserLauncher,
        BootstrapRedirectFile redirectFile,
        IOptions<LifecycleOptions> options,
        IHostApplicationLifetime lifetime,
        ILogger<BrowserOpeningService> logger)
    {
        _bootstrapUrlFactory = bootstrapUrlFactory;
        _browserLauncher = browserLauncher;
        _redirectFile = redirectFile;
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
        _redirectFile.Delete();
        return Task.CompletedTask;
    }

    internal void OpenBrowser()
    {
        if (_lifetime.ApplicationStopping.IsCancellationRequested)
        {
            return;
        }

        try
        {
            var bootstrapUrl = _bootstrapUrlFactory.Create();
            if (bootstrapUrl is null)
            {
                _logger.LogWarning("The browser was not opened: the server has no loopback HTTP address");
                return;
            }

            var redirectAddress = _redirectFile.Create(bootstrapUrl);
            _browserLauncher.Open(redirectAddress);
            var origin = bootstrapUrl.GetLeftPart(UriPartial.Authority);
            _logger.LogInformation($"Opened the browser at {origin}");
        }
        catch (Win32Exception exception)
        {
            _redirectFile.Delete();
            _logger.LogError($"The browser could not be opened, native error code {exception.NativeErrorCode}");
        }
        catch (InvalidOperationException)
        {
            _redirectFile.Delete();
            _logger.LogError("The browser could not be opened: no browser command is available");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _redirectFile.Delete();
            _logger.LogError("The browser could not be opened: the private launch file is unavailable");
        }
    }
}
