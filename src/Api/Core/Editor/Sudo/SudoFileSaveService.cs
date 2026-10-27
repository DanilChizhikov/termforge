namespace DTech.Core.Editor.Sudo;

public sealed class SudoFileSaveService
{
    private readonly ISudoEditorSessionOpener _opener;
    private readonly EditorPathResolver _paths;
    private readonly TextContentCodec _codec;
    private readonly RemoteFileSaver _saver;
    private readonly RemoteFileRollback _rollback;
    private readonly RemoteDocumentReader _documents;
    private readonly EditorOperationRecorder _recorder;

    public SudoFileSaveService(
        ISudoEditorSessionOpener opener,
        EditorPathResolver paths,
        TextContentCodec codec,
        RemoteFileSaver saver,
        RemoteFileRollback rollback,
        RemoteDocumentReader documents,
        EditorOperationRecorder recorder)
    {
        _opener = opener;
        _paths = paths;
        _codec = codec;
        _saver = saver;
        _rollback = rollback;
        _documents = documents;
        _recorder = recorder;
    }

    public async Task<EditorResult<JournaledSave>> SaveAsync(SudoSaveRequest request, CancellationToken cancellationToken)
    {
        var document = request.Document;
        var location = _paths.Resolve(document.Path);
        if (!location.IsSuccess)
        {
            return EditorResult<JournaledSave>.Failure(location.Error);
        }

        var content = _codec.Encode(document.Content);
        if (!content.IsSuccess)
        {
            return EditorResult<JournaledSave>.Failure(content.Error);
        }

        var opened = await _opener.OpenAsync(request.ConnectionId, request.Credentials, cancellationToken);
        if (!opened.IsSuccess)
        {
            return EditorResult<JournaledSave>.Failure(opened.Error);
        }

        await using var session = opened.Value;
        var start = new EditorOperationStart(EditorOperationRecorder.SaveKind, session.ServerName, location.Value.Path)
        {
            Elevated = true,
        };
        var operationId = await _recorder.StartAsync(start, cancellationToken);
        var save = new RemoteSaveRequest(location.Value, content.Value, document.ExpectedVersion);
        var result = await _saver.SaveAsync(session.Client, save, cancellationToken);
        await _recorder.CompleteAsync(operationId, result);
        if (!result.IsSuccess)
        {
            return EditorResult<JournaledSave>.Failure(result.Error);
        }

        var journaled = new JournaledSave(result.Value, operationId);
        return EditorResult<JournaledSave>.Success(journaled);
    }

    public async Task<EditorResult<JournaledSave>> RollbackAsync(SudoRollbackRequest request, CancellationToken cancellationToken)
    {
        var document = request.Document;
        var location = _paths.Resolve(document.Path);
        if (!location.IsSuccess)
        {
            return EditorResult<JournaledSave>.Failure(location.Error);
        }

        var opened = await _opener.OpenAsync(request.ConnectionId, request.Credentials, cancellationToken);
        if (!opened.IsSuccess)
        {
            return EditorResult<JournaledSave>.Failure(opened.Error);
        }

        await using var session = opened.Value;
        var start = new EditorOperationStart(EditorOperationRecorder.RollbackKind, session.ServerName, location.Value.Path)
        {
            BackupName = document.BackupName,
            Elevated = true,
        };
        var operationId = await _recorder.StartAsync(start, cancellationToken);
        var rollback = new RollbackRequest(location.Value, document.BackupName, document.ExpectedVersion);
        var result = await _rollback.RollbackAsync(session.Client, rollback, cancellationToken);
        await _recorder.CompleteAsync(operationId, result);
        if (!result.IsSuccess)
        {
            return EditorResult<JournaledSave>.Failure(result.Error);
        }

        var journaled = new JournaledSave(result.Value, operationId);
        if (!result.Value.IsSaved)
        {
            return EditorResult<JournaledSave>.Success(journaled);
        }

        var reopened = await _documents.ReadAsync(session.Client, location.Value.Path, cancellationToken);
        var withDocument = journaled with { Document = reopened.IsSuccess ? reopened.Value : null };
        return EditorResult<JournaledSave>.Success(withDocument);
    }
}
