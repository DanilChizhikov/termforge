using DTech.Core.Transfers;
using DTech.Core.Transfers.Execution;
using DTech.Core.Transfers.Queue;

namespace DTech.Entrypoint.Transfers;

internal sealed class TransferQueueHostedService : BackgroundService
{
    private readonly TransferQueue _queue;
    private readonly TransferRunner _runner;
    private readonly TransferControl _control;
    private readonly TransferOptions _options;
    private readonly ILogger<TransferQueueHostedService> _logger;

    public TransferQueueHostedService(
        TransferQueue queue,
        TransferRunner runner,
        TransferControl control,
        TransferOptions options,
        ILogger<TransferQueueHostedService> logger)
    {
        _queue = queue;
        _runner = runner;
        _control = control;
        _options = options;
        _logger = logger;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _control.CancelAllAsync();
        using var timeout = new CancellationTokenSource(_options.ShutdownTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        await base.StopAsync(linked.Token);
        if (ExecuteTask is { IsCompleted: false })
        {
            _logger.LogWarning($"Transfers did not finish cleanup within {_options.ShutdownTimeout.TotalSeconds} seconds of shutdown");
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var parallelism = _options.EffectiveParallelism;
        var workers = new Task[parallelism];
        for (var i = 0; i < parallelism; i++)
        {
            workers[i] = RunWorkerAsync(stoppingToken);
        }

        _logger.LogInformation($"Transfer queue started with {parallelism} parallel workers");
        return Task.WhenAll(workers);
    }

    public async Task WaitForWorkersAsync(CancellationToken cancellationToken)
    {
        var workers = ExecuteTask;
        if (workers is null)
        {
            return;
        }

        await workers.WaitAsync(cancellationToken);
    }

    private async Task RunWorkerAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var job in _queue.Pending.ReadAllAsync(stoppingToken))
            {
                await RunJobAsync(job);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogDebug("Transfer worker stopped");
        }
    }

    private async Task RunJobAsync(TransferJob job)
    {
        try
        {
            await _runner.RunAsync(job);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, $"Transfer {job.Id} stopped unexpectedly: {exception.GetType().Name}");
            await FailSafelyAsync(job, exception);
        }
    }

    private async Task FailSafelyAsync(TransferJob job, Exception exception)
    {
        try
        {
            await _runner.FailUnexpectedlyAsync(job, exception);
        }
        catch (Exception recordingException)
        {
            _logger.LogError(recordingException, $"Recording the failure of transfer {job.Id} failed: {recordingException.GetType().Name}");
        }
    }
}
