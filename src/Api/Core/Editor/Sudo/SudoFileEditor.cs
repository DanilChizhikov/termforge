namespace DTech.Core.Editor.Sudo;

public sealed class SudoFileEditor
{
    private readonly ISudoEditorSessionOpener _opener;
    private readonly EditorPathResolver _paths;
    private readonly RemoteDocumentReader _documents;
    private readonly BackupCatalog _backups;

    public SudoFileEditor(ISudoEditorSessionOpener opener, EditorPathResolver paths, RemoteDocumentReader documents, BackupCatalog backups)
    {
        _opener = opener;
        _paths = paths;
        _documents = documents;
        _backups = backups;
    }

    public async Task<EditorResult<RemoteDocument>> OpenAsync(SudoDocumentRequest request, CancellationToken cancellationToken)
    {
        var location = _paths.Resolve(request.Path);
        if (!location.IsSuccess)
        {
            return EditorResult<RemoteDocument>.Failure(location.Error);
        }

        var opened = await _opener.OpenAsync(request.ConnectionId, request.Credentials, cancellationToken);
        if (!opened.IsSuccess)
        {
            return EditorResult<RemoteDocument>.Failure(opened.Error);
        }

        await using var session = opened.Value;
        return await _documents.ReadAsync(session.Client, location.Value.Path, cancellationToken);
    }

    public async Task<EditorResult<RemoteDocument>> ReadBackupAsync(SudoBackupRequest request, CancellationToken cancellationToken)
    {
        var reference = request.Reference;
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

        var opened = await _opener.OpenAsync(request.ConnectionId, request.Credentials, cancellationToken);
        if (!opened.IsSuccess)
        {
            return EditorResult<RemoteDocument>.Failure(opened.Error);
        }

        await using var session = opened.Value;
        return await _documents.ReadAsync(session.Client, backup.Value.Path, cancellationToken);
    }
}
