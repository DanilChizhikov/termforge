namespace DTech.Entrypoint.Lifecycle;

internal sealed class ApplicationExitCoordinator : IDisposable
{
    private readonly WorkAdmissionGate _gate;
    private readonly GracefulShutdown _shutdown;
    private readonly ActiveTransferWatcher _transferWatcher;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<ApplicationExitCoordinator> _logger;
    private readonly Lock _sync = new();
    private CancellationTokenSource? _waiting;

    public ApplicationExitCoordinator(
        WorkAdmissionGate gate,
        GracefulShutdown shutdown,
        ActiveTransferWatcher transferWatcher,
        IHostApplicationLifetime lifetime,
        ILogger<ApplicationExitCoordinator> logger)
    {
        _gate = gate;
        _shutdown = shutdown;
        _transferWatcher = transferWatcher;
        _lifetime = lifetime;
        _logger = logger;
    }

    public ApplicationExitState State => _gate.State;

    public bool TryBegin(ExitTransferPolicy policy)
    {
        lock (_sync)
        {
            var waitForTransfers = policy == ExitTransferPolicy.WaitForTransfers && _transferWatcher.HasActiveTransfers();
            if (waitForTransfers)
            {
                return _gate.TryBeginWaiting();
            }

            if (!_gate.TryBeginShutdown())
            {
                return false;
            }

            _waiting?.Cancel();
            return true;
        }
    }

    public void Start()
    {
        lock (_sync)
        {
            if (_gate.State == ApplicationExitState.WaitingForTransfers)
            {
                _waiting?.Dispose();
                _waiting = new CancellationTokenSource();
                var waitingToken = _waiting.Token;
                _ = WaitThenExitAsync(waitingToken);
                return;
            }
        }

        _ = ExitAsync();
    }

    public bool TryCancelWaiting()
    {
        lock (_sync)
        {
            if (!_gate.TryReopen())
            {
                return false;
            }

            _waiting?.Cancel();
            _logger.LogInformation("Pending application exit was cancelled");
            return true;
        }
    }

    public void Dispose()
    {
        _waiting?.Dispose();
    }

    private async Task WaitThenExitAsync(CancellationToken waitingToken)
    {
        _logger.LogInformation("Application exit is waiting for active transfers to finish");
        try
        {
            await _transferWatcher.WaitUntilIdleAsync(waitingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        lock (_sync)
        {
            if (waitingToken.IsCancellationRequested || !_gate.TryBeginShutdown())
            {
                return;
            }
        }

        await ExitAsync();
    }

    private async Task ExitAsync()
    {
        _logger.LogInformation("Application exit started");
        try
        {
            await _shutdown.RunAsync();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, $"Graceful shutdown failed: {exception.GetType().Name}");
        }

        _lifetime.StopApplication();
    }
}
