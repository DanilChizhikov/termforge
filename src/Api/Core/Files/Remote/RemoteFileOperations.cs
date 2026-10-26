using DTech.Core.Connections;

namespace DTech.Core.Files.Remote;

public sealed class RemoteFileOperations(IRemoteConnectionOpener opener, RemotePathNormalizer normalizer)
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

        var target = normalizer.Combine(parent, request.NewName);
        var rename = new RemoteRenameRequest(source.Value, target);
        var operation = new RemoteOperation(connectionId, source.Value, (client, token) => client.RenameAsync(rename, token));
        return await RunAsync(operation, cancellationToken);
    }

    public async Task<FileResult> DeleteAsync(Guid connectionId, DeleteRequest request, CancellationToken cancellationToken)
    {
        if (!request.Confirmed)
        {
            var confirmation = FileError.ConfirmationRequired(request.Path);
            return FileResult.Failure(confirmation);
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
