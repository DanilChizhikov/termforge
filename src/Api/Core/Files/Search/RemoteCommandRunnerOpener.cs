using DTech.Core.Diagnostics;
using DTech.Core.SavedConnections;

namespace DTech.Core.Files.Search;

public sealed class RemoteCommandRunnerOpener(ISavedConnectionRepository repository, ConnectionTargetFactory targetFactory, IRemoteCommandRunnerFactory runnerFactory) : IRemoteCommandRunnerOpener
{
    public async Task<FileResult<IRemoteCommandRunner>> OpenAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        var all = await repository.ListAsync(cancellationToken);
        var connection = JumpHostGuard.Find(all, connectionId);
        if (connection is null)
        {
            var missing = new FileError(FileErrorCode.NotFound, $"Saved connection {connectionId} was not found.");
            return FileResult<IRemoteCommandRunner>.Failure(missing);
        }

        var target = await targetFactory.CreateAsync(connection, all, cancellationToken);
        if (!target.IsSuccess)
        {
            var unavailable = new FileError(FileErrorCode.ConnectionFailed, target.Error.Message);
            return FileResult<IRemoteCommandRunner>.Failure(unavailable);
        }

        var connected = await runnerFactory.ConnectAsync(target.Value, cancellationToken);
        if (!connected.IsSuccess)
        {
            var failure = FileErrorMapper.FromSsh(connected.Error!, connection.Details.Host);
            return FileResult<IRemoteCommandRunner>.Failure(failure);
        }

        return FileResult<IRemoteCommandRunner>.Success(connected.Value);
    }
}
