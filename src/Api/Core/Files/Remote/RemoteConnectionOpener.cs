using DTech.Core.SavedConnections;

namespace DTech.Core.Files.Remote;

public sealed class RemoteConnectionOpener(ISavedConnectionRepository repository, ConnectionTargetFactory targetFactory, IRemoteFileClientFactory clientFactory) : IRemoteConnectionOpener
{
    public async Task<FileResult<IRemoteFileClient>> OpenAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        var all = await repository.ListAsync(cancellationToken);
        var connection = JumpHostGuard.Find(all, connectionId);
        if (connection is null)
        {
            var missing = new FileError(FileErrorCode.NotFound, $"Saved connection {connectionId} was not found.");
            return FileResult<IRemoteFileClient>.Failure(missing);
        }

        var target = await targetFactory.CreateAsync(connection, all, cancellationToken);
        if (!target.IsSuccess)
        {
            var unavailable = new FileError(FileErrorCode.ConnectionFailed, target.Error.Message);
            return FileResult<IRemoteFileClient>.Failure(unavailable);
        }

        var connected = await clientFactory.ConnectAsync(target.Value, cancellationToken);
        if (!connected.IsSuccess)
        {
            var failure = FileErrorMapper.FromSsh(connected.Error!, connection.Details.Host);
            return FileResult<IRemoteFileClient>.Failure(failure);
        }

        return FileResult<IRemoteFileClient>.Success(connected.Value);
    }
}
