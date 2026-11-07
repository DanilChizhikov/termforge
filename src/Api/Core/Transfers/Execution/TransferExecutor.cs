using DTech.Core.Files;
using DTech.Core.Transfers.Queue;

namespace DTech.Core.Transfers.Execution;

public sealed class TransferExecutor(ITransferConnectionOpener opener, TransferStrategySelector strategies, TransferFileCopier copier)
{
    public async Task<TransferOutcome> RunAsync(TransferJob job)
    {
        var cancellationToken = job.CancellationToken;
        FileResult<TransferConnections> opened;
        try
        {
            opened = await TransferConnections.OpenAsync(opener, job.Request, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return TransferOutcome.Cancelled;
        }

        if (!opened.IsSuccess)
        {
            return cancellationToken.IsCancellationRequested ? TransferOutcome.Cancelled : CreateFailure(job, opened.Error);
        }

        await using var connections = opened.Value;
        var strategy = strategies.For(job.Request.Direction);
        var context = connections.Context(job.Request);
        var file = new TransferFileContext(strategy, context, job);
        var result = await TransferSafelyAsync(file, cancellationToken);
        if (result.IsSuccess)
        {
            return TransferOutcome.Succeeded;
        }

        return await ResolveFailureAsync(file, result.Error);
    }

    private async Task<FileResult> TransferSafelyAsync(TransferFileContext file, CancellationToken cancellationToken)
    {
        try
        {
            return await TransferAsync(file, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            var cancelled = new FileError(FileErrorCode.Cancelled, "The transfer was cancelled.");
            return FileResult.Failure(cancelled);
        }
    }

    private async Task<FileResult> TransferAsync(TransferFileContext file, CancellationToken cancellationToken)
    {
        var planned = await file.Strategy.PlanAsync(file.Context, cancellationToken);
        if (!planned.IsSuccess)
        {
            return FileResult.Failure(planned.Error);
        }

        var plan = planned.Value;
        if (file.Context.Request.Direction == TransferDirection.RemoteToRemote && !file.Job.CheckSourceVersions(plan.Items))
        {
            return RemoteTransferVerifier.RestartRequired();
        }
        file.Job.Plan(plan.TotalBytes, plan.FileCount);
        for (var i = 0; i < plan.Items.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = plan.Items[i];
            var result = item.IsDirectory
                ? await file.Strategy.CreateDirectoryAsync(file.Context, item.TargetPath, cancellationToken)
                : await copier.CopyAsync(file, item, cancellationToken);
            if (!result.IsSuccess)
            {
                return result;
            }
        }

        return FileResult.Success();
    }

    private static async Task<TransferOutcome> ResolveFailureAsync(TransferFileContext file, FileError error)
    {
        var job = file.Job;
        var partialPath = job.PartialPath;
        if (job.CancellationToken.IsCancellationRequested)
        {
            await DeletePartialAsync(file, partialPath);
            return TransferOutcome.Cancelled;
        }

        var retryable = TransferFailureClassifier.IsRetryable(error.Code);
        if (!retryable)
        {
            await DeletePartialAsync(file, partialPath);
        }

        var keptPartial = retryable ? partialPath : null;
        var partialBytes = keptPartial is null ? 0 : job.CurrentFileBytes;
        var progress = job.Progress();
        var failure = new TransferFailure(error.Message, retryable, progress) { PartialBytes = partialBytes };
        return TransferOutcome.Failed(failure, keptPartial);
    }

    private static async Task DeletePartialAsync(TransferFileContext file, string? partialPath)
    {
        if (partialPath is null)
        {
            return;
        }

        var deleted = await file.Strategy.DeletePartialAsync(file.Context, partialPath, CancellationToken.None);
        if (deleted.IsSuccess || deleted.Error.Code == FileErrorCode.NotFound)
        {
            file.Job.ForgetPartialPath(partialPath);
        }
    }

    private static TransferOutcome CreateFailure(TransferJob job, FileError error)
    {
        var retryable = TransferFailureClassifier.IsRetryable(error.Code);
        var progress = job.Progress();
        var failure = new TransferFailure(error.Message, retryable, progress);
        return TransferOutcome.Failed(failure, null);
    }
}
