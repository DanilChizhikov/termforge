using DTech.Core.SavedConnections;

namespace DTech.Core.Editor.Sudo;

public sealed class RemoteShellConnector(ISavedConnectionRepository repository, ConnectionTargetFactory targetFactory, IRemoteShellCommandRunnerFactory runnerFactory)
{
    public async Task<EditorResult<ConnectedShellRunner>> ConnectAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        var all = await repository.ListAsync(cancellationToken);
        var connection = JumpHostGuard.Find(all, connectionId);
        if (connection is null)
        {
            var missing = new EditorError(EditorErrorCode.NotFound, $"Saved connection {connectionId} was not found.");
            return EditorResult<ConnectedShellRunner>.Failure(missing);
        }

        var target = await targetFactory.CreateAsync(connection, all, cancellationToken);
        if (!target.IsSuccess)
        {
            var unavailable = new EditorError(EditorErrorCode.ConnectionFailed, target.Error.Message);
            return EditorResult<ConnectedShellRunner>.Failure(unavailable);
        }

        var connected = await runnerFactory.ConnectAsync(target.Value, cancellationToken);
        if (!connected.IsSuccess)
        {
            var failure = EditorErrorMapper.FromSsh(connected.Error!, connection.Details.Host);
            return EditorResult<ConnectedShellRunner>.Failure(failure);
        }

        var runner = new ConnectedShellRunner(connected.Value, connection.Details.Name);
        return EditorResult<ConnectedShellRunner>.Success(runner);
    }
}
