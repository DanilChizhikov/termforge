using DTech.Core.Connections;
using DTech.Core.Safety;

namespace DTech.Core.Files.Remote;

public sealed class RemoteFileOperations(IRemoteConnectionOpener opener, RemotePathNormalizer normalizer, ServerWriteGate writeGate)
{
    public async Task<FileResult> CreateDirectoryAsync(Guid connectionId, CreateDirectoryRequest request, CancellationToken cancellationToken)
    {
        var nameError = FileNameValidator.Validate(request.Name);
        if (nameError is not null)
        {
            return FileResult.Failure(nameError);
        }

        var parent = normalizer.Normalize(request.ParentPath);
        if (!parent.IsSuccess)
        {
            return FileResult.Failure(parent.Error);
        }

        var writeIntent = new WriteIntent(connectionId);
        var denied = await CheckWriteAsync(writeIntent, request.ParentPath, cancellationToken);
        if (denied is not null)
        {
            return FileResult.Failure(denied);
        }

        var target = normalizer.Combine(parent.Value, request.Name);
        var operation = new RemoteOperation(connectionId, target, (client, token) => client.CreateDirectoryAsync(target, token));
        return await RunAsync(operation, cancellationToken);
    }

    public async Task<FileResult> RenameAsync(Guid connectionId, RenameRequest request, CancellationToken cancellationToken)
    {
        var nameError = FileNameValidator.Validate(request.NewName);
        if (nameError is not null)
        {
            return FileResult.Failure(nameError);
        }

        var source = normalizer.Normalize(request.Path);
        if (!source.IsSuccess)
        {
            return FileResult.Failure(source.Error);
        }

        var parent = normalizer.GetParent(source.Value);
        if (parent is null)
        {
            var root = FileError.Invalid("The filesystem root cannot be renamed.");
            return FileResult.Failure(root);
        }

        var writeIntent = new WriteIntent(connectionId);
        var denied = await CheckWriteAsync(writeIntent, request.Path, cancellationToken);
        if (denied is not null)
        {
            return FileResult.Failure(denied);
        }

        var target = normalizer.Combine(parent, request.NewName);
        var rename = new RemoteRenameRequest(source.Value, target);
        var operation = new RemoteOperation(connectionId, source.Value, (client, token) => client.RenameAsync(rename, token));
        return await RunAsync(operation, cancellationToken);
    }

    public async Task<FileResult> DeleteAsync(Guid connectionId, RemoteDeleteRequest request, CancellationToken cancellationToken)
    {
        var writeIntent = new WriteIntent(connectionId)
        {
            Operation = DangerousOperation.RemoteDelete,
            Target = request.Path,
            ConfirmationToken = request.ConfirmationToken,
        };
        var denied = await CheckWriteAsync(writeIntent, request.Path, cancellationToken);
        if (denied is not null)
        {
            return FileResult.Failure(denied);
        }

        var source = normalizer.Normalize(request.Path);
        if (!source.IsSuccess)
        {
            return FileResult.Failure(source.Error);
        }

        if (normalizer.GetParent(source.Value) is null)
        {
            var root = FileError.Invalid("The filesystem root cannot be deleted.");
            return FileResult.Failure(root);
        }

        var operation = new RemoteOperation(connectionId, source.Value, (client, token) => client.DeleteAsync(source.Value, token));
        return await RunAsync(operation, cancellationToken);
    }

    private async Task<FileError?> CheckWriteAsync(WriteIntent intent, string path, CancellationToken cancellationToken)
    {
        var denial = await writeGate.CheckAsync(intent, cancellationToken);
        return denial switch
        {
            WriteDenial.ReadOnlyServer => FileError.ReadOnlyServer(),
            WriteDenial.ConfirmationRequired => FileError.ConfirmationRequired(path),
            _ => null,
        };
    }

    private async Task<FileResult> RunAsync(RemoteOperation operation, CancellationToken cancellationToken)
    {
        var opened = await opener.OpenAsync(operation.ConnectionId, cancellationToken);
        if (!opened.IsSuccess)
        {
            return FileResult.Failure(opened.Error);
        }

        await using var client = opened.Value;
        var result = await operation.Execute(client, cancellationToken);
        if (result.IsSuccess)
        {
            return FileResult.Success();
        }

        var failure = FileErrorMapper.FromSsh(result.Error!, operation.Path);
        return FileResult.Failure(failure);
    }

    private readonly record struct RemoteOperation(Guid ConnectionId, string Path, Func<IRemoteFileClient, CancellationToken, Task<SshResult>> Execute);
}
