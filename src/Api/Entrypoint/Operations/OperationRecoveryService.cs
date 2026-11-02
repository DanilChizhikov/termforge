using DTech.Core.Operations;
using DTech.Core.Transfers;
using DTech.Core.Transfers.Queue;

namespace DTech.Entrypoint.Operations;

internal sealed class OperationRecoveryService
{
    private readonly IOperationMaintenance _maintenance;
    private readonly TransferQueue _queue;
    private readonly TimeProvider _timeProvider;
    private readonly IEnumerable<IOperationRetryHandler> _handlers;
    private readonly ILogger<OperationRecoveryService> _logger;

    public OperationRecoveryService(
        IOperationMaintenance maintenance,
        TransferQueue queue,
        TimeProvider timeProvider,
        IEnumerable<IOperationRetryHandler> handlers,
        ILogger<OperationRecoveryService> logger)
    {
        _maintenance = maintenance;
        _queue = queue;
        _timeProvider = timeProvider;
        _handlers = handlers;
        _logger = logger;
    }

    public async Task RecoverAsync(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var kinds = _handlers.Select(handler => handler.Kind).ToArray();
        var policy = new OperationRecoveryPolicy(now, kinds);
        await _maintenance.RecoverAsync(policy, cancellationToken);
        var operations = await _maintenance.ListPendingCleanupAsync(cancellationToken);
        for (var i = 0; i < operations.Count; i++)
        {
            Restore(operations[i]);
        }

        var retention = OperationRetentionService.Policy(now);
        await _maintenance.PruneAsync(retention, cancellationToken);
    }

    private void Restore(Operation operation)
    {
        var state = TransferRecoveryState.TryRead(operation);
        if (state is null)
        {
            _logger.LogWarning($"Operation {operation.Id} has unreadable transfer recovery metadata; its partial files were kept.");
            return;
        }

        var job = new TransferJob(operation.Id, state.Request, state.TargetPath);
        job.RestorePartials(state.PartialPaths);
        if (operation.Status == OperationStatus.Succeeded)
        {
            job.Succeed();
        }
        else if (operation.Status == OperationStatus.Cancelled)
        {
            job.MarkCancelled();
        }
        else
        {
            var progress = new TransferProgress(state.PartialBytes, 0, 0, 0, 0, null);
            var failure = new TransferFailure(operation.Error ?? "The transfer has partial files to clean up.", operation.Retryable, progress)
            {
                PartialBytes = state.PartialBytes,
            };
            job.Fail(failure, null);
        }

        _queue.RestoreFinished(job);
    }
}
