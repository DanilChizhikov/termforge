using DTech.Core.Files;
using DTech.Core.Files.Remote;
using DTech.Core.Safety;

namespace DTech.Core.Transfers.Execution;

public sealed class RemoteToRemoteTransferStrategy : ITransferStrategy
{
    private readonly RemotePathNormalizer _paths;
    private readonly RemoteTransferPlanner _planner;
    private readonly UploadTransferStrategy _upload;
    private readonly ServerWriteGate _writeGate;
    private readonly RemoteTransferVerifier _verifier;

    public RemoteToRemoteTransferStrategy(RemotePathNormalizer paths, RemoteTransferPlanner planner, UploadTransferStrategy upload, ServerWriteGate writeGate, RemoteTransferVerifier verifier)
    {
        _paths = paths;
        _planner = planner;
        _upload = upload;
        _writeGate = writeGate;
        _verifier = verifier;
    }

    public TransferDirection Direction => TransferDirection.RemoteToRemote;

    public async Task<FileResult<TransferInspection>> InspectAsync(TransferContext context, CancellationToken cancellationToken)
    {
        var root = Root(context.Request);
        if (!root.IsSuccess) return FileResult<TransferInspection>.Failure(root.Error);
        var source = await context.Client.StatAsync(root.Value.SourcePath, cancellationToken);
        if (!source.IsSuccess)
        {
            var error = FileErrorMapper.FromSsh(source.Error!, root.Value.SourcePath);
            return FileResult<TransferInspection>.Failure(error);
        }

        var target = await context.TargetClient!.StatAsync(context.Request.TargetDirectory, cancellationToken);
        if (!target.IsSuccess)
        {
            var error = FileErrorMapper.FromSsh(target.Error!, context.Request.TargetDirectory);
            return FileResult<TransferInspection>.Failure(error);
        }
        if (RemoteTransferPlanner.ResolveKind(target.Value) != RemoteFileKind.Directory)
        {
            var error = FileError.NotADirectory(context.Request.TargetDirectory);
            return FileResult<TransferInspection>.Failure(error);
        }
        var existing = await FindTargetSizeAsync(context, root.Value.TargetPath, cancellationToken);
        if (!existing.IsSuccess) return FileResult<TransferInspection>.Failure(existing.Error);
        var inspection = new TransferInspection(root.Value.TargetPath, existing.Value is not null);
        return FileResult<TransferInspection>.Success(inspection);
    }

    public Task<FileResult<TransferPlan>> PlanAsync(TransferContext context, CancellationToken cancellationToken)
    {
        var root = Root(context.Request);
        if (!root.IsSuccess) return Task.FromResult(FileResult<TransferPlan>.Failure(root.Error));
        var planning = new RemotePlanContext(context.Client, _paths.Combine);
        return _planner.PlanAsync(planning, root.Value, cancellationToken);
    }

    public async Task<FileResult> CreateDirectoryAsync(TransferContext context, string targetPath, CancellationToken cancellationToken)
    {
        if (await IsReadOnlyAsync(context, cancellationToken)) return FileResult.Failure(FileError.ReadOnlyServer());
        var destination = Destination(context);
        return await _upload.CreateDirectoryAsync(destination, targetPath, cancellationToken);
    }

    public Task<FileResult<long?>> FindTargetSizeAsync(TransferContext context, string targetSidePath, CancellationToken cancellationToken)
    {
        var destination = Destination(context);
        return _upload.FindTargetSizeAsync(destination, targetSidePath, cancellationToken);
    }

    public string GetPartialPath(TransferItem item, string partialName) => _upload.GetPartialPath(item, partialName);

    public async Task<FileResult> CopyAsync(TransferContext context, TransferCopy copy, CancellationToken cancellationToken)
    {
        if (await IsReadOnlyAsync(context, cancellationToken)) return FileResult.Failure(FileError.ReadOnlyServer());
        var before = await _verifier.CheckSourceAsync(context, copy.Item, cancellationToken);
        if (!before.IsSuccess) return before;
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        await using var relay = new TransferRelayStream();
        var read = new RemoteReadRequest(copy.Item.SourcePath, relay, copy.Offset);
        var write = new RemoteWriteRequest(relay, copy.PartialPath, copy.Offset) { Progress = copy.Progress };
        var reading = ReadAsync(context.Client, read, stop);
        var writing = WriteAsync(context.TargetClient!, write, stop);
        try
        {
            await Task.WhenAll(reading, writing);
            var result = !writing.Result.IsSuccess && writing.Result.Error!.Kind != DTech.Core.Connections.SshErrorKind.Cancelled
                ? writing.Result : !reading.Result.IsSuccess ? reading.Result : writing.Result;
            if (!result.IsSuccess)
            {
                var error = FileErrorMapper.FromSsh(result.Error!, copy.Item.SourcePath);
                return FileResult.Failure(error);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            DTech.Core.Connections.SshResult? failed = reading.IsCompletedSuccessfully && !reading.Result.IsSuccess ? reading.Result
                : writing.IsCompletedSuccessfully && !writing.Result.IsSuccess ? writing.Result : null;
            var error = failed is null
                ? new FileError(FileErrorCode.ConnectionFailed, "The server-to-server transfer was interrupted.")
                : FileErrorMapper.FromSsh(failed.Value.Error!, copy.Item.SourcePath);
            return FileResult.Failure(error);
        }
        finally
        {
            await stop.CancelAsync();
        }
        return await _verifier.CheckSourceAsync(context, copy.Item, cancellationToken);
    }

    public async Task<FileResult> CommitAsync(TransferContext context, TransferCopy copy, CancellationToken cancellationToken)
    {
        if (await IsReadOnlyAsync(context, cancellationToken)) return FileResult.Failure(FileError.ReadOnlyServer());
        var checkedSource = await _verifier.CheckSourceAsync(context, copy.Item, cancellationToken);
        if (!checkedSource.IsSuccess) return checkedSource;
        var size = await FindTargetSizeAsync(context, copy.PartialPath, cancellationToken);
        if (!size.IsSuccess) return FileResult.Failure(size.Error);
        if (size.Value != copy.Item.Size) return RemoteTransferVerifier.RestartRequired();
        var destination = Destination(context);
        return await _upload.CommitAsync(destination, copy, cancellationToken);
    }

    public async Task<FileResult> DeletePartialAsync(TransferContext context, string partialPath, CancellationToken cancellationToken)
    {
        if (await IsReadOnlyAsync(context, cancellationToken)) return FileResult.Failure(FileError.ReadOnlyServer());
        var destination = Destination(context);
        return await _upload.DeletePartialAsync(destination, partialPath, cancellationToken);
    }

    private Task<bool> IsReadOnlyAsync(TransferContext context, CancellationToken cancellationToken)
    {
        return _writeGate.IsReadOnlyAsync(context.Request.DestinationConnectionId, cancellationToken);
    }

    private static TransferContext Destination(TransferContext context)
    {
        return new TransferContext(context.TargetClient ?? context.Client, context.Request);
    }

    private FileResult<TransferRoot> Root(TransferRequest request)
    {
        var source = _paths.Normalize(request.SourcePath);
        var directory = _paths.Normalize(request.TargetDirectory);
        if (!source.IsSuccess) return FileResult<TransferRoot>.Failure(source.Error);
        if (!directory.IsSuccess) return FileResult<TransferRoot>.Failure(directory.Error);
        var name = source.Value.Split('/')[^1];
        if (name.Length == 0) return FileResult<TransferRoot>.Failure(FileError.Invalid("The filesystem root cannot be transferred."));
        var target = _paths.Combine(directory.Value, name);
        if (request.ConnectionId == request.DestinationConnectionId
            && (target == source.Value || target.StartsWith(source.Value + "/", StringComparison.Ordinal)))
        {
            return FileResult<TransferRoot>.Failure(FileError.Invalid("Cannot copy a path into itself."));
        }
        var root = new TransferRoot(source.Value, target, name);
        return FileResult<TransferRoot>.Success(root);
    }

    private static async Task<DTech.Core.Connections.SshResult> ReadAsync(IRemoteFileClient client, RemoteReadRequest request, CancellationTokenSource stop)
    {
        var relay = (TransferRelayStream)request.Destination;
        try
        {
            var result = await client.ReadFileAsync(request, stop.Token);
            if (!result.IsSuccess) await stop.CancelAsync();
            return result;
        }
        catch
        {
            await stop.CancelAsync();
            throw;
        }
        finally { relay.Complete(); }
    }

    private static async Task<DTech.Core.Connections.SshResult> WriteAsync(IRemoteFileClient client, RemoteWriteRequest request, CancellationTokenSource stop)
    {
        try
        {
            var result = await client.WriteFileAsync(request, stop.Token);
            if (!result.IsSuccess) await stop.CancelAsync();
            return result;
        }
        catch
        {
            await stop.CancelAsync();
            throw;
        }
    }
}
