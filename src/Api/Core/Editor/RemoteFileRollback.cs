namespace DTech.Core.Editor;

public sealed class RemoteFileRollback(BackupCatalog catalog, RemoteFileSnapshotReader snapshots, RemoteFileSaver saver)
{
    public async Task<EditorResult<RemoteSaveResult>> RollbackAsync(IRemoteEditorFileClient client, RollbackRequest request, CancellationToken cancellationToken)
    {
        var backup = catalog.Resolve(request.Location, request.BackupName);
        if (!backup.IsSuccess)
        {
            return EditorResult<RemoteSaveResult>.Failure(backup.Error);
        }

        var snapshot = await snapshots.ReadAsync(client, backup.Value.Path, cancellationToken);
        if (!snapshot.IsSuccess)
        {
            return EditorResult<RemoteSaveResult>.Failure(snapshot.Error);
        }

        if (snapshot.Value is null)
        {
            var missing = EditorError.NotFound(backup.Value.Path);
            return EditorResult<RemoteSaveResult>.Failure(missing);
        }

        var save = new RemoteSaveRequest(request.Location, snapshot.Value.Content, request.ExpectedVersion);
        return await saver.SaveAsync(client, save, cancellationToken);
    }
}
