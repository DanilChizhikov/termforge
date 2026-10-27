namespace DTech.Core.Editor;

public sealed class RemoteFileEditor
{
    private readonly IRemoteEditorConnectionOpener _opener;
    private readonly EditorPathResolver _paths;
    private readonly RemoteDocumentReader _documents;
    private readonly BackupCatalog _backups;

    public RemoteFileEditor(IRemoteEditorConnectionOpener opener, EditorPathResolver paths, RemoteDocumentReader documents, BackupCatalog backups)
    {
        _opener = opener;
        _paths = paths;
        _documents = documents;
        _backups = backups;
    }

    public async Task<EditorResult<RemoteDocument>> OpenAsync(Guid connectionId, string? path, CancellationToken cancellationToken)
    {
        var location = _paths.Resolve(path);
        if (!location.IsSuccess)
        {
            return EditorResult<RemoteDocument>.Failure(location.Error);
        }

        var opened = await _opener.OpenAsync(connectionId, cancellationToken);
        if (!opened.IsSuccess)
        {
            return EditorResult<RemoteDocument>.Failure(opened.Error);
        }

        await using var session = opened.Value;
        return await _documents.ReadAsync(session.Client, location.Value.Path, cancellationToken);
    }

    public async Task<EditorResult<IReadOnlyList<FileBackup>>> ListBackupsAsync(Guid connectionId, string? path, CancellationToken cancellationToken)
    {
        var location = _paths.Resolve(path);
        if (!location.IsSuccess)
        {
            return EditorResult<IReadOnlyList<FileBackup>>.Failure(location.Error);
        }

        var opened = await _opener.OpenAsync(connectionId, cancellationToken);
        if (!opened.IsSuccess)
        {
            return EditorResult<IReadOnlyList<FileBackup>>.Failure(opened.Error);
        }

        await using var session = opened.Value;
        return await _backups.ListAsync(session.Client, location.Value, cancellationToken);
    }

    public async Task<EditorResult<RemoteDocument>> ReadBackupAsync(Guid connectionId, BackupReference reference, CancellationToken cancellationToken)
    {
        var location = _paths.Resolve(reference.Path);
        if (!location.IsSuccess)
        {
            return EditorResult<RemoteDocument>.Failure(location.Error);
        }

        var backup = _backups.Resolve(location.Value, reference.BackupName);
        if (!backup.IsSuccess)
        {
            return EditorResult<RemoteDocument>.Failure(backup.Error);
        }

        var opened = await _opener.OpenAsync(connectionId, cancellationToken);
        if (!opened.IsSuccess)
        {
            return EditorResult<RemoteDocument>.Failure(opened.Error);
        }

        await using var session = opened.Value;
        return await _documents.ReadAsync(session.Client, backup.Value.Path, cancellationToken);
    }
}
