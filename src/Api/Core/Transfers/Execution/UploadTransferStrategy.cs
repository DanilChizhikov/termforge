using DTech.Core.Connections;
using DTech.Core.Files;
using DTech.Core.Files.Local;
using DTech.Core.Files.Remote;

namespace DTech.Core.Transfers.Execution;

public sealed class UploadTransferStrategy(LocalPathResolver localPaths, RemotePathNormalizer remotePaths, LocalTransferPlanner planner) : ITransferStrategy
{
    private const int LocalBufferSize = 64 * 1024;

    public TransferDirection Direction => TransferDirection.Upload;

    public async Task<FileResult<TransferInspection>> InspectAsync(TransferContext context, CancellationToken cancellationToken)
    {
        var root = ResolveRoot(context.Request);
        if (!root.IsSuccess)
        {
            return FileResult<TransferInspection>.Failure(root.Error);
        }

        var source = root.Value.SourcePath;
        if (!File.Exists(source) && !Directory.Exists(source))
        {
            var missing = FileError.NotFound(source);
            return FileResult<TransferInspection>.Failure(missing);
        }

        var targetDirectory = await context.Client.StatAsync(context.Request.TargetDirectory, cancellationToken);
        if (!targetDirectory.IsSuccess)
        {
            var directoryError = FileErrorMapper.FromSsh(targetDirectory.Error!, context.Request.TargetDirectory);
            return FileResult<TransferInspection>.Failure(directoryError);
        }

        if (RemoteTransferPlanner.ResolveKind(targetDirectory.Value) != RemoteFileKind.Directory)
        {
            var notDirectory = FileError.NotADirectory(context.Request.TargetDirectory);
            return FileResult<TransferInspection>.Failure(notDirectory);
        }

        var target = root.Value.TargetPath;
        var existing = await FindTargetSizeAsync(context, target, cancellationToken);
        if (!existing.IsSuccess)
        {
            return FileResult<TransferInspection>.Failure(existing.Error);
        }

        var inspection = new TransferInspection(target, existing.Value is not null);
        return FileResult<TransferInspection>.Success(inspection);
    }

    public Task<FileResult<TransferPlan>> PlanAsync(TransferContext context, CancellationToken cancellationToken)
    {
        var root = ResolveRoot(context.Request);
        if (!root.IsSuccess)
        {
            var failure = FileResult<TransferPlan>.Failure(root.Error);
            return Task.FromResult(failure);
        }

        var plan = planner.Plan(root.Value, remotePaths.Combine, cancellationToken);
        return Task.FromResult(plan);
    }

    public async Task<FileResult> CreateDirectoryAsync(TransferContext context, string targetPath, CancellationToken cancellationToken)
    {
        var created = await context.Client.CreateDirectoryAsync(targetPath, cancellationToken);
        if (created.IsSuccess)
        {
            return FileResult.Success();
        }

        if (created.Error!.Kind != SshErrorKind.RemotePathExists)
        {
            var createError = FileErrorMapper.FromSsh(created.Error, targetPath);
            return FileResult.Failure(createError);
        }

        var existing = await context.Client.StatAsync(targetPath, cancellationToken);
        if (existing.IsSuccess && RemoteTransferPlanner.ResolveKind(existing.Value) == RemoteFileKind.Directory)
        {
            return FileResult.Success();
        }

        var notDirectory = FileError.NotADirectory(targetPath);
        return FileResult.Failure(notDirectory);
    }

    public async Task<FileResult<long?>> FindTargetSizeAsync(TransferContext context, string targetSidePath, CancellationToken cancellationToken)
    {
        var stat = await context.Client.StatAsync(targetSidePath, cancellationToken);
        if (stat.IsSuccess)
        {
            long? size = stat.Value.Size;
            return FileResult<long?>.Success(size);
        }

        if (stat.Error!.Kind == SshErrorKind.RemotePathNotFound)
        {
            return FileResult<long?>.Success(null);
        }

        var error = FileErrorMapper.FromSsh(stat.Error, targetSidePath);
        return FileResult<long?>.Failure(error);
    }

    public string GetPartialPath(TransferItem item, string partialName)
    {
        var parent = remotePaths.GetParent(item.TargetPath) ?? RemotePathNormalizer.RootPath;
        return remotePaths.Combine(parent, partialName);
    }

    public async Task<FileResult> CopyAsync(TransferContext context, TransferCopy copy, CancellationToken cancellationToken)
    {
        FileStream source;
        try
        {
            source = new FileStream(copy.Item.SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, LocalBufferSize, true);
            source.Seek(copy.Offset, SeekOrigin.Begin);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            var openError = LocalIoErrors.Map(exception, copy.Item.SourcePath);
            return FileResult.Failure(openError);
        }

        await using (source)
        {
            var request = new RemoteWriteRequest(source, copy.PartialPath, copy.Offset) { Progress = copy.Progress };
            var written = await context.Client.WriteFileAsync(request, cancellationToken);
            if (written.IsSuccess)
            {
                return FileResult.Success();
            }

            var writeError = FileErrorMapper.FromSsh(written.Error!, copy.Item.TargetPath);
            return FileResult.Failure(writeError);
        }
    }

    public async Task<FileResult> CommitAsync(TransferContext context, TransferCopy copy, CancellationToken cancellationToken)
    {
        var rename = new RemoteRenameRequest(copy.PartialPath, copy.Item.TargetPath);
        var replaced = await context.Client.ReplaceAsync(rename, cancellationToken);
        if (replaced.IsSuccess)
        {
            return FileResult.Success();
        }

        var error = FileErrorMapper.FromSsh(replaced.Error!, copy.Item.TargetPath);
        return FileResult.Failure(error);
    }

    public async Task<FileResult> DeletePartialAsync(TransferContext context, string partialPath, CancellationToken cancellationToken)
    {
        var deleted = await context.Client.DeleteAsync(partialPath, cancellationToken);
        if (deleted.IsSuccess || deleted.Error!.Kind == SshErrorKind.RemotePathNotFound)
        {
            return FileResult.Success();
        }

        var error = FileErrorMapper.FromSsh(deleted.Error, partialPath);
        return FileResult.Failure(error);
    }

    private FileResult<TransferRoot> ResolveRoot(TransferRequest request)
    {
        var source = localPaths.Resolve(request.SourcePath);
        if (!source.IsSuccess)
        {
            return FileResult<TransferRoot>.Failure(source.Error);
        }

        var targetDirectory = remotePaths.Normalize(request.TargetDirectory);
        if (!targetDirectory.IsSuccess)
        {
            return FileResult<TransferRoot>.Failure(targetDirectory.Error);
        }

        var name = Path.GetFileName(source.Value);
        var nameError = FileNameValidator.Validate(name);
        if (nameError is not null)
        {
            return FileResult<TransferRoot>.Failure(nameError);
        }

        var target = remotePaths.Combine(targetDirectory.Value, name);
        var root = new TransferRoot(source.Value, target, name);
        return FileResult<TransferRoot>.Success(root);
    }
}
