using Microsoft.Extensions.Options;

namespace DTech.Entrypoint.Lifecycle;

internal sealed class IdleShutdownService : BackgroundService
{
    private readonly LifecycleOptions _options;
    private readonly IBrowserPresence _browserPresence;
    private readonly ActiveTransferWatcher _transferWatcher;
    private readonly ApplicationExitCoordinator _exitCoordinator;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<IdleShutdownService> _logger;

    public IdleShutdownService(
        IOptions<LifecycleOptions> options,
        IBrowserPresence browserPresence,
        ActiveTransferWatcher transferWatcher,
        ApplicationExitCoordinator exitCoordinator,
        TimeProvider timeProvider,
        ILogger<IdleShutdownService> logger)
    {
        _options = options.Value;
        _browserPresence = browserPresence;
        _transferWatcher = transferWatcher;
        _exitCoordinator = exitCoordinator;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.IsIdleShutdownEnabled)
        {
            return;
        }

        var monitor = new IdleShutdownMonitor(_options.IdleShutdownTimeout);
        _logger.LogInformation($"Idle shutdown is enabled after {_options.IdleShutdownMinutes} minutes without a connected browser");
        using var timer = new PeriodicTimer(_options.IdleCheckInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                if (TryShutdownWhenIdle(monitor))
                {
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogDebug("Idle shutdown monitor stopped");
        }
    }

    private bool TryShutdownWhenIdle(IdleShutdownMonitor monitor)
    {
        var isIdle = !_browserPresence.IsAnyBrowserConnected && !_transferWatcher.HasActiveTransfers();
        var now = _timeProvider.GetUtcNow();
        if (!monitor.ShouldShutdown(isIdle, now))
        {
            return false;
        }

        if (!_exitCoordinator.TryBegin(ExitTransferPolicy.WaitForTransfers))
        {
            return true;
        }

        _logger.LogInformation($"No browser was connected for {_options.IdleShutdownMinutes} minutes: shutting down");
        _exitCoordinator.Start();
        return true;
    }
}
