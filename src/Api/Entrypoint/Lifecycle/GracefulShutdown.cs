using Microsoft.Extensions.Options;

namespace DTech.Entrypoint.Lifecycle;

internal sealed class GracefulShutdown
{
    private readonly IShutdownStep[] _steps;
    private readonly TimeSpan _timeout;
    private readonly ILogger<GracefulShutdown> _logger;
    private readonly Lock _gate = new();
    private Task<ShutdownOutcome>? _run;

    public GracefulShutdown(IEnumerable<IShutdownStep> steps, IOptions<LifecycleOptions> options, ILogger<GracefulShutdown> logger)
    {
        _steps = steps.OrderBy(step => step.Stage).ToArray();
        _timeout = options.Value.ShutdownTimeout;
        _logger = logger;
    }

    public TimeSpan Timeout => _timeout;

    public Task<ShutdownOutcome> RunAsync()
    {
        lock (_gate)
        {
            _run ??= RunStepsAsync();
            return _run;
        }
    }

    private async Task<ShutdownOutcome> RunStepsAsync()
    {
        await Task.Yield();
        using var budget = new CancellationTokenSource(_timeout);
        var token = budget.Token;
        for (var i = 0; i < _steps.Length; i++)
        {
            var step = _steps[i];
            var completed = await TryRunStepAsync(step, token);
            if (!completed)
            {
                _logger.LogWarning($"Graceful shutdown stopped at stage {step.Stage}: the {_timeout.TotalSeconds} second shutdown timeout elapsed");
                return ShutdownOutcome.TimedOut;
            }
        }

        _logger.LogInformation($"Graceful shutdown finished {_steps.Length} steps");
        return ShutdownOutcome.Completed;
    }

    private async Task<bool> TryRunStepAsync(IShutdownStep step, CancellationToken token)
    {
        try
        {
            var running = step.RunAsync(token);
            await running.WaitAsync(token);
            return true;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, $"Shutdown stage {step.Stage} failed: {exception.GetType().Name}");
            return !token.IsCancellationRequested;
        }
    }
}
