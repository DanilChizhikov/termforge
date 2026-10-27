using DTech.Core.Files;
using DTech.Core.Operations;
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

    public TransferControl(TransferQueue queue, ITransferJournal journal, ITransferNotifier notifier, ITransferConnectionOpener opener, TransferStrategySelector strategies)
    {
        _queue = queue;
        _journal = journal;
        _notifier = notifier;
        _opener = opener;
        _strategies = strategies;
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

        var deleted = await DeletePartialAsync(job, cancellationToken);
        if (!deleted.IsSuccess)
        {
            return deleted;
        }

        job.ForgetPartial();
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
            if (OperationStateMachine.IsTerminal(job.Status) && job.PartialPath is null && _queue.Remove(job.Id))
            {
                await _notifier.TransferRemoved(job.Id, CancellationToken.None);
            }
        }
    }

    private async Task<FileResult> DeletePartialAsync(TransferJob job, CancellationToken cancellationToken)
    {
        var partialPath = job.PartialPath;
        if (partialPath is null)
        {
            return FileResult.Success();
        }

        var opened = await _opener.OpenAsync(job.Request.ConnectionId, cancellationToken);
        if (!opened.IsSuccess)
        {
            return FileResult.Failure(opened.Error);
        }

        await using var client = opened.Value;
        var strategy = _strategies.For(job.Request.Direction);
        var context = new TransferContext(client, job.Request);
        return await strategy.DeletePartialAsync(context, partialPath, cancellationToken);
    }
}
