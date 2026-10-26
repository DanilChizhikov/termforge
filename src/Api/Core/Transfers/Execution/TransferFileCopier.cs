using DTech.Core.Files;

namespace DTech.Core.Transfers.Execution;

public sealed class TransferFileCopier
{
    public async Task<FileResult> CopyAsync(TransferFileContext file, TransferItem item, CancellationToken cancellationToken)
    {
        var strategy = file.Strategy;
        var context = file.Context;
        var job = file.Job;
        var existing = await strategy.FindTargetSizeAsync(context, item.TargetPath, cancellationToken);
        if (!existing.IsSuccess)
        {
            return FileResult.Failure(existing.Error);
        }

        if (existing.Value is not null)
        {
            if (job.ResumeExisting && existing.Value == item.Size)
            {
                job.CompleteFile(item.Size);
                return FileResult.Success();
            }

            if (!context.Request.Overwrite)
            {
                var conflict = FileError.AlreadyExists(item.TargetPath);
                return FileResult.Failure(conflict);
            }
        }

        var partialPath = CreatePartialPath(file, item);
        var candidate = new PartialCandidate(partialPath, item.Size);
        var offset = await FindResumeOffsetAsync(file, candidate, cancellationToken);
        job.BeginFile(partialPath, offset);
        var progress = new TransferFileProgress(job, offset);
        var copy = new TransferCopy(item, partialPath, offset) { Progress = progress };
        var copied = await strategy.CopyAsync(context, copy, cancellationToken);
        if (!copied.IsSuccess)
        {
            return copied;
        }

        var committed = await strategy.CommitAsync(context, copy, cancellationToken);
        if (!committed.IsSuccess)
        {
            return committed;
        }

        job.CompleteFile(item.Size);
        return FileResult.Success();
    }

    private static string CreatePartialPath(TransferFileContext file, TransferItem item)
    {
        var request = file.Context.Request;
        var identity = new TransferPartialIdentity
        {
            Direction = request.Direction,
            ConnectionId = request.ConnectionId,
            SourcePath = item.SourcePath,
            TargetPath = item.TargetPath,
            SourceSize = item.Size,
            SourceModifiedUtc = item.ModifiedUtc,
        };
        var partialName = TransferPartialName.Create(identity, item.TargetName);
        return file.Strategy.GetPartialPath(item, partialName);
    }

    private static async Task<long> FindResumeOffsetAsync(TransferFileContext file, PartialCandidate candidate, CancellationToken cancellationToken)
    {
        var partial = await file.Strategy.FindTargetSizeAsync(file.Context, candidate.Path, cancellationToken);
        if (!partial.IsSuccess || partial.Value is not { } size)
        {
            return 0;
        }

        return size > 0 && size <= candidate.SourceSize ? size : 0;
    }

    private readonly record struct PartialCandidate(string Path, long SourceSize);
}
