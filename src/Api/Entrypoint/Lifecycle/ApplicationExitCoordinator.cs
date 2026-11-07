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
    private long _requestVersion;
    private long _startedVersion;
    private bool _ownsWaiting;

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
        return TryBegin(policy, out _);
    }

    public bool TryBegin(ExitTransferPolicy policy, out long requestVersion)
    {
        lock (_sync)
        {
            requestVersion = 0;
            if (_gate.State == ApplicationExitState.WaitingForTransfers && !_ownsWaiting)
            {
                return false;
            }
            var waitForTransfers = policy == ExitTransferPolicy.WaitForTransfers && _transferWatcher.HasActiveTransfers();
            if (waitForTransfers)
            {
                if (!_gate.TryBeginWaiting())
                {
                    return false;
                }
            }
            else
            {
                if (!_gate.TryBeginShutdown())
                {
                    return false;
                }

                _waiting?.Cancel();
            }

            _ownsWaiting = waitForTransfers;
            requestVersion = ++_requestVersion;
            return true;
        }
    }

    public void Start()
    {
        lock (_sync)
        {
            Start(_requestVersion);
        }
    }

    public void Start(long requestVersion)
    {
        lock (_sync)
        {
            if (requestVersion != _requestVersion || requestVersion == _startedVersion)
            {
                return;
            }

            if (_gate.State == ApplicationExitState.WaitingForTransfers)
            {
                _startedVersion = requestVersion;
                _waiting?.Dispose();
                _waiting = new CancellationTokenSource();
                var waitingToken = _waiting.Token;
                _ = WaitThenExitAsync(waitingToken, requestVersion);
                return;
            }

            if (_gate.State == ApplicationExitState.ShuttingDown)
            {
                _startedVersion = requestVersion;
                _ = ExitAsync();
            }
        }
    }

    public bool TryCancelWaiting()
    {
        lock (_sync)
        {
            if (!_ownsWaiting || !_gate.TryReopen())
            {
                return false;
            }

            _waiting?.Cancel();
            _ownsWaiting = false;
            _logger.LogInformation("Pending application exit was cancelled");
            return true;
        }
    }

    public void Dispose()
    {
        _waiting?.Dispose();
    }

    private async Task WaitThenExitAsync(CancellationToken waitingToken, long requestVersion)
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
            if (waitingToken.IsCancellationRequested || requestVersion != _requestVersion || _gate.State != ApplicationExitState.WaitingForTransfers || !_gate.TryBeginShutdown())
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
