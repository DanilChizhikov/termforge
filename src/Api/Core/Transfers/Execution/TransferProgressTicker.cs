using DTech.Core.Transfers.Queue;

namespace DTech.Core.Transfers.Execution;

public sealed class TransferProgressTicker(TimeProvider timeProvider, TransferOptions options, ITransferNotifier notifier)
{
    public async Task RunAsync(TransferJob job, CancellationToken cancellationToken)
    {
        var meter = new TransferRateMeter();
        var startedAt = timeProvider.GetTimestamp();
        using var timer = new PeriodicTimer(options.ProgressInterval, timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                var elapsed = timeProvider.GetElapsedTime(startedAt);
                var progress = job.Progress();
                var rate = meter.Sample(elapsed, progress.TransferredBytes, progress.TotalBytes);
                job.UpdateRate(rate.BytesPerSecond, rate.RemainingSeconds);
                var snapshot = job.Snapshot();
                await notifier.TransferChanged(snapshot, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
    }
}
