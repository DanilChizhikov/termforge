using DTech.Core.Files;
using DTech.Core.Operations;
using DTech.Core.Safety;
using DTech.Core.Transfers.Execution;
using DTech.Core.Transfers.Queue;

namespace DTech.Core.Transfers;

public sealed class TransferControl
{
    private readonly TransferQueue _queue;
    private readonly ITransferJournal _journal;
    private readonly ITransferNotifier _notifier;
    private readonly ITransferConnectionOpener _opener;
    private readonly TransferStrategySelector _strategies;
    private readonly ServerWriteGate? _writeGate;

    public TransferControl(TransferQueue queue, ITransferJournal journal, ITransferNotifier notifier, ITransferConnectionOpener opener, TransferStrategySelector strategies, ServerWriteGate? writeGate = null)
    {
        _queue = queue;
        _journal = journal;
        _notifier = notifier;
        _opener = opener;
        _strategies = strategies;
        _writeGate = writeGate;
    }

    public IReadOnlyList<TransferSnapshot> List()
    {
        var jobs = _queue.List();
        var snapshots = new List<TransferSnapshot>(jobs.Count);
        for (var i = 0; i < jobs.Count; i++)
        {
            var snapshot = jobs[i].Snapshot();
            snapshots.Add(snapshot);
        }

        return snapshots;
    }

    public async Task FinishAsync(TransferJob job)
    {
        var removed = _queue.Finish(job);
        for (var i = 0; i < removed.Count; i++)
        {
            await _notifier.TransferRemoved(removed[i], CancellationToken.None);
        }
    }

    public async Task<TransferCancelOutcome> CancelAsync(Guid id)
    {
        var job = _queue.Find(id);
        if (job is null)
        {
            return TransferCancelOutcome.NotFound;
        }

        var outcome = job.RequestCancel();
        if (outcome == TransferCancelOutcome.CancelledWhileQueued)
        {
            await _journal.CancelAsync(id, CancellationToken.None);
            var snapshot = job.Snapshot();
            await _notifier.TransferChanged(snapshot, CancellationToken.None);
        }

        return outcome;
    }

    public async Task CancelAllAsync()
    {
        _queue.Close();
        var jobs = _queue.List();
        for (var i = 0; i < jobs.Count; i++)
        {
            await CancelAsync(jobs[i].Id);
        }
    }

    public async Task<FileResult> DiscardAsync(Guid id, CancellationToken cancellationToken)
    {
        var job = _queue.Find(id);
        if (job is null)
        {
            var missing = new FileError(FileErrorCode.NotFound, $"Transfer {id} was not found.");
            return FileResult.Failure(missing);
        }

        if (!OperationStateMachine.IsTerminal(job.Status))
        {
            var active = FileError.Invalid("Cancel the transfer before discarding it.");
            return FileResult.Failure(active);
        }

        TransferTarget[] targets = [new(job.Request, job.TargetPath)];
        var reserved = _queue.TryReserve(targets);
        using var reservation = reserved.Reservation;
        if (reservation is null)
        {
            var busy = FileError.Invalid("Wait for the active transfer to finish before discarding its partial files.");
            return FileResult.Failure(busy);
        }

        var deleted = await DeletePartialAsync(job, cancellationToken);
        if (!deleted.IsSuccess)
        {
            return deleted;
        }

        job.ForgetPartial();
        await _journal.CheckpointAsync(job, CancellationToken.None);
        _queue.Remove(id);
        await _notifier.TransferRemoved(id, CancellationToken.None);
        return FileResult.Success();
    }

    public async Task ClearFinishedAsync()
    {
        var jobs = _queue.List();
        for (var i = 0; i < jobs.Count; i++)
        {
            var job = jobs[i];
            if (OperationStateMachine.IsTerminal(job.Status) && !job.HasPartial && _queue.Remove(job.Id))
            {
                await _notifier.TransferRemoved(job.Id, CancellationToken.None);
            }
        }
    }

    private async Task<FileResult> DeletePartialAsync(TransferJob job, CancellationToken cancellationToken)
    {
        var recovery = job.CaptureRecovery();
        if (recovery.PartialPaths.Count == 0)
        {
            return FileResult.Success();
        }

        if (job.Request.Direction == TransferDirection.Download)
        {
            return DeleteLocalPartials(recovery.PartialPaths);
        }

        if (_writeGate is not null && await _writeGate.IsReadOnlyAsync(job.Request.DestinationConnectionId, cancellationToken))
        {
            var readOnly = FileError.ReadOnlyServer();
            return FileResult.Failure(readOnly);
        }

        var opened = await _opener.OpenAsync(job.Request.DestinationConnectionId, cancellationToken);
        if (!opened.IsSuccess)
        {
            return FileResult.Failure(opened.Error);
        }

        await using var client = opened.Value;
        var strategy = _strategies.For(job.Request.Direction);
        var context = new TransferContext(client, job.Request) { TargetClient = client };
        for (var i = 0; i < recovery.PartialPaths.Count; i++)
        {
            var deleted = await strategy.DeletePartialAsync(context, recovery.PartialPaths[i], cancellationToken);
            if (!deleted.IsSuccess && deleted.Error.Code != FileErrorCode.NotFound)
            {
                return deleted;
            }
        }

        return FileResult.Success();
    }

    private static FileResult DeleteLocalPartials(IReadOnlyList<string> paths)
    {
        for (var i = 0; i < paths.Count; i++)
        {
            try
            {
                File.Delete(paths[i]);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                var error = DTech.Core.Files.Local.LocalIoErrors.Map(exception, paths[i]);
                return FileResult.Failure(error);
            }
        }

        return FileResult.Success();
    }
}
