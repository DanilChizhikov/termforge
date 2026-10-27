using DTech.Core.Safety;

namespace DTech.Core.Editor;

public sealed class RemoteFileSaveService
{
    private readonly IRemoteEditorConnectionOpener _opener;
    private readonly EditorPathResolver _paths;
    private readonly TextContentCodec _codec;
    private readonly RemoteFileSaver _saver;
    private readonly RemoteFileRollback _rollback;
    private readonly RemoteDocumentReader _documents;
    private readonly EditorOperationRecorder _recorder;
    private readonly ServerWriteGate _writeGate;

    public RemoteFileSaveService(
        IRemoteEditorConnectionOpener opener,
        EditorPathResolver paths,
        TextContentCodec codec,
        RemoteFileSaver saver,
        RemoteFileRollback rollback,
        RemoteDocumentReader documents,
        EditorOperationRecorder recorder,
        ServerWriteGate writeGate)
    {
        _opener = opener;
        _paths = paths;
        _codec = codec;
        _saver = saver;
        _rollback = rollback;
        _documents = documents;
        _recorder = recorder;
        _writeGate = writeGate;
    }

    public async Task<EditorResult<JournaledSave>> SaveAsync(Guid connectionId, SaveDocumentRequest request, CancellationToken cancellationToken)
    {
        var location = _paths.Resolve(request.Path);
        if (!location.IsSuccess)
        {
            return EditorResult<JournaledSave>.Failure(location.Error);
        }

        var content = _codec.Encode(request.Content);
        if (!content.IsSuccess)
        {
            return EditorResult<JournaledSave>.Failure(content.Error);
        }

        var writeIntent = new WriteIntent(connectionId);
        var saveDenial = await _writeGate.CheckAsync(writeIntent, cancellationToken);
        if (saveDenial is not null)
        {
            var denied = EditorError.FromDenial(saveDenial.Value, request.Path);
            return EditorResult<JournaledSave>.Failure(denied);
        }

        var opened = await _opener.OpenAsync(connectionId, cancellationToken);
        if (!opened.IsSuccess)
        {
            return EditorResult<JournaledSave>.Failure(opened.Error);
        }

        await using var session = opened.Value;
        var start = new EditorOperationStart(EditorOperationRecorder.SaveKind, session.ServerName, location.Value.Path);
        var operationId = await _recorder.StartAsync(start, cancellationToken);
        var save = new RemoteSaveRequest(location.Value, content.Value, request.ExpectedVersion);
        var result = await _saver.SaveAsync(session.Client, save, cancellationToken);
        await _recorder.CompleteAsync(operationId, result);
        if (!result.IsSuccess)
        {
            return EditorResult<JournaledSave>.Failure(result.Error);
        }

        var journaled = new JournaledSave(result.Value, operationId);
        return EditorResult<JournaledSave>.Success(journaled);
    }

    public async Task<EditorResult<JournaledSave>> RollbackAsync(Guid connectionId, RollbackDocumentRequest request, CancellationToken cancellationToken)
    {
        var location = _paths.Resolve(request.Path);
        if (!location.IsSuccess)
        {
            return EditorResult<JournaledSave>.Failure(location.Error);
        }

        var rollbackIntent = new WriteIntent(connectionId)
        {
            Operation = DangerousOperation.FileRollback,
            Target = request.Path ?? string.Empty,
            ConfirmationToken = request.ConfirmationToken,
        };
        var rollbackDenial = await _writeGate.CheckAsync(rollbackIntent, cancellationToken);
        if (rollbackDenial is not null)
        {
            var denied = EditorError.FromDenial(rollbackDenial.Value, request.Path);
            return EditorResult<JournaledSave>.Failure(denied);
        }

        var opened = await _opener.OpenAsync(connectionId, cancellationToken);
        if (!opened.IsSuccess)
        {
            return EditorResult<JournaledSave>.Failure(opened.Error);
        }

        await using var session = opened.Value;
        var start = new EditorOperationStart(EditorOperationRecorder.RollbackKind, session.ServerName, location.Value.Path)
        {
            BackupName = request.BackupName,
        };
        var operationId = await _recorder.StartAsync(start, cancellationToken);
        var rollback = new RollbackRequest(location.Value, request.BackupName, request.ExpectedVersion);
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

        var document = await _documents.ReadAsync(session.Client, location.Value.Path, cancellationToken);
        var withDocument = journaled with { Document = document.IsSuccess ? document.Value : null };
        return EditorResult<JournaledSave>.Success(withDocument);
    }
}
