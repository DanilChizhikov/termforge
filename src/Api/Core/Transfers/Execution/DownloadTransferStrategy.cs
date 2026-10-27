using DTech.Core.Files;
using DTech.Core.Files.Local;
using DTech.Core.Files.Remote;

namespace DTech.Core.Transfers.Execution;

public sealed class DownloadTransferStrategy(LocalPathResolver localPaths, RemotePathNormalizer remotePaths, RemoteTransferPlanner planner) : ITransferStrategy
{
    private const int LocalBufferSize = 64 * 1024;

    public TransferDirection Direction => TransferDirection.Download;

    public async Task<FileResult<TransferInspection>> InspectAsync(TransferContext context, CancellationToken cancellationToken)
    {
        var root = ResolveRoot(context.Request);
        if (!root.IsSuccess)
        {
            return FileResult<TransferInspection>.Failure(root.Error);
        }

        var source = await context.Client.StatAsync(root.Value.SourcePath, cancellationToken);
        if (!source.IsSuccess)
        {
            var sourceError = FileErrorMapper.FromSsh(source.Error!, root.Value.SourcePath);
            return FileResult<TransferInspection>.Failure(sourceError);
        }

        var kind = RemoteTransferPlanner.ResolveKind(source.Value);
        if (kind is not (RemoteFileKind.File or RemoteFileKind.Directory))
        {
            var unsupported = FileError.Invalid($"Only files and directories can be transferred: {root.Value.SourcePath}");
            return FileResult<TransferInspection>.Failure(unsupported);
        }

        var targetDirectory = Path.GetDirectoryName(root.Value.TargetPath)!;
        if (!Directory.Exists(targetDirectory))
        {
            var missingDirectory = FileError.NotFound(targetDirectory);
            return FileResult<TransferInspection>.Failure(missingDirectory);
        }

        var exists = LocalFileInfo.Open(root.Value.TargetPath) is not null;
        var inspection = new TransferInspection(root.Value.TargetPath, exists);
        return FileResult<TransferInspection>.Success(inspection);
    }

    public async Task<FileResult<TransferPlan>> PlanAsync(TransferContext context, CancellationToken cancellationToken)
    {
        var root = ResolveRoot(context.Request);
        if (!root.IsSuccess)
        {
            return FileResult<TransferPlan>.Failure(root.Error);
        }

        var planContext = new RemotePlanContext(context.Client, Path.Combine);
        return await planner.PlanAsync(planContext, root.Value, cancellationToken);
    }

    public Task<FileResult> CreateDirectoryAsync(TransferContext context, string targetPath, CancellationToken cancellationToken)
    {
        var result = RunLocal(() => Directory.CreateDirectory(targetPath), targetPath);
        return Task.FromResult(result);
    }

    public Task<FileResult<long?>> FindTargetSizeAsync(TransferContext context, string targetSidePath, CancellationToken cancellationToken)
    {
        var info = LocalFileInfo.Open(targetSidePath);
        long? size = info switch
        {
            null => null,
            FileInfo file => file.Length,
            _ => -1,
        };
        var result = FileResult<long?>.Success(size);
        return Task.FromResult(result);
    }

    public string GetPartialPath(TransferItem item, string partialName)
    {
        var parent = Path.GetDirectoryName(item.TargetPath)!;
        return Path.Combine(parent, partialName);
    }

    public async Task<FileResult> CopyAsync(TransferContext context, TransferCopy copy, CancellationToken cancellationToken)
    {
        FileStream destination;
        try
        {
            var mode = copy.Offset == 0 ? FileMode.Create : FileMode.OpenOrCreate;
            destination = new FileStream(copy.PartialPath, mode, FileAccess.Write, FileShare.None, LocalBufferSize, true);
            destination.SetLength(copy.Offset);
            destination.Seek(copy.Offset, SeekOrigin.Begin);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            var openError = LocalIoErrors.Map(exception, copy.PartialPath);
            return FileResult.Failure(openError);
        }

        await using (destination)
        {
            var request = new RemoteReadRequest(copy.Item.SourcePath, destination, copy.Offset) { Progress = copy.Progress };
            var read = await context.Client.ReadFileAsync(request, cancellationToken);
            if (read.IsSuccess)
            {
                return FileResult.Success();
            }

            var readError = FileErrorMapper.FromSsh(read.Error!, copy.Item.SourcePath);
            return FileResult.Failure(readError);
        }
    }

    public Task<FileResult> CommitAsync(TransferContext context, TransferCopy copy, CancellationToken cancellationToken)
    {
        var result = RunLocal(() => File.Move(copy.PartialPath, copy.Item.TargetPath, true), copy.Item.TargetPath);
        return Task.FromResult(result);
    }

    public Task<FileResult> DeletePartialAsync(TransferContext context, string partialPath, CancellationToken cancellationToken)
    {
        var result = RunLocal(() => File.Delete(partialPath), partialPath);
        return Task.FromResult(result);
    }

    private static FileResult RunLocal(Action action, string path)
    {
        try
        {
            action();
            return FileResult.Success();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            var error = LocalIoErrors.Map(exception, path);
            return FileResult.Failure(error);
        }
    }

    private FileResult<TransferRoot> ResolveRoot(TransferRequest request)
    {
        var source = remotePaths.Normalize(request.SourcePath);
        if (!source.IsSuccess)
        {
            return FileResult<TransferRoot>.Failure(source.Error);
        }

        var targetDirectory = localPaths.Resolve(request.TargetDirectory);
        if (!targetDirectory.IsSuccess)
        {
            return FileResult<TransferRoot>.Failure(targetDirectory.Error);
        }

        var parent = remotePaths.GetParent(source.Value);
        if (parent is null)
        {
            var root = FileError.Invalid("The remote filesystem root cannot be downloaded as a whole.");
            return FileResult<TransferRoot>.Failure(root);
        }

        var name = source.Value[(parent.Length == 1 ? 1 : parent.Length + 1)..];
        var nameError = LocalNameGuard.Validate(name);
        if (nameError is not null)
        {
            return FileResult<TransferRoot>.Failure(nameError);
        }

        var target = Path.Combine(targetDirectory.Value, name);
        var transferRoot = new TransferRoot(source.Value, target, name);
        return FileResult<TransferRoot>.Success(transferRoot);
    }
}
