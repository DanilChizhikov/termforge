using DTech.Core.SavedConnections;

namespace DTech.Core.Editor;

public sealed class RemoteEditorConnectionOpener(ISavedConnectionRepository repository, ConnectionTargetFactory targetFactory, IRemoteEditorFileClientFactory clientFactory) : IRemoteEditorConnectionOpener
{
    public async Task<EditorResult<RemoteEditorSession>> OpenAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        var all = await repository.ListAsync(cancellationToken);
        var connection = JumpHostGuard.Find(all, connectionId);
        if (connection is null)
        {
            var missing = new EditorError(EditorErrorCode.NotFound, $"Saved connection {connectionId} was not found.");
            return EditorResult<RemoteEditorSession>.Failure(missing);
        }

        var target = await targetFactory.CreateAsync(connection, all, cancellationToken);
        if (!target.IsSuccess)
        {
            var unavailable = new EditorError(EditorErrorCode.ConnectionFailed, target.Error.Message);
            return EditorResult<RemoteEditorSession>.Failure(unavailable);
        }

        var connected = await clientFactory.ConnectAsync(target.Value, cancellationToken);
        if (!connected.IsSuccess)
        {
            var failure = EditorErrorMapper.FromSsh(connected.Error!, connection.Details.Host);
            return EditorResult<RemoteEditorSession>.Failure(failure);
        }

        var session = new RemoteEditorSession(connected.Value, connection.Details.Name);
        return EditorResult<RemoteEditorSession>.Success(session);
    }
}
