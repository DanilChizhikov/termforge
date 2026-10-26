using DTech.Core.Operations;
using DTech.Core.Transfers.Queue;

namespace DTech.Core.Transfers.Execution;

public sealed class TransferRunner
{
    private readonly TransferExecutor _executor;
    private readonly TransferProgressTicker _ticker;
    private readonly ITransferJournal _journal;
    private readonly ITransferNotifier _notifier;

    public TransferRunner(TransferExecutor executor, TransferProgressTicker ticker, ITransferJournal journal, ITransferNotifier notifier)
    {
        _executor = executor;
        _ticker = ticker;
        _journal = journal;
        _notifier = notifier;
    }

    public async Task RunAsync(TransferJob job)
    {
        if (!job.TryStart())
        {
            return;
        }

        await _journal.StartAsync(job.Id, CancellationToken.None);
        var started = job.Snapshot();
        await _notifier.TransferChanged(started, CancellationToken.None);
        var outcome = await ExecuteWithProgressAsync(job);
        await CompleteAsync(job, outcome);
    }

    public async Task FailUnexpectedlyAsync(TransferJob job, Exception exception)
    {
        var progress = job.Progress();
        var failure = new TransferFailure($"The transfer stopped unexpectedly: {exception.Message}", true, progress);
        var outcome = TransferOutcome.Failed(failure, job.PartialPath);
        await CompleteAsync(job, outcome);
    }

    private async Task<TransferOutcome> ExecuteWithProgressAsync(TransferJob job)
    {
        using var tickerStop = new CancellationTokenSource();
        var ticking = _ticker.RunAsync(job, tickerStop.Token);
        try
        {
            return await _executor.RunAsync(job);
        }
        finally
        {
            await tickerStop.CancelAsync();
            await ticking;
        }
    }

    private async Task CompleteAsync(TransferJob job, TransferOutcome outcome)
    {
        switch (outcome.Status)
        {
            case OperationStatus.Succeeded:
                job.Succeed();
                var progress = job.Progress();
                await _journal.SucceedAsync(job.Id, progress, CancellationToken.None);
                break;
            case OperationStatus.Failed:
                job.Fail(outcome.Failure!, outcome.PartialPath);
                await _journal.FailAsync(job.Id, outcome.Failure!, CancellationToken.None);
                break;
            default:
                job.MarkCancelled();
                await _journal.CancelAsync(job.Id, CancellationToken.None);
                break;
        }

        var finished = job.Snapshot();
        await _notifier.TransferChanged(finished, CancellationToken.None);
    }
}
