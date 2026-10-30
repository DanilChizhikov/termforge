using DTech.Core.AiBridge.Approvals;
using DTech.Core.Editor;

namespace DTech.Core.AiBridge.Files;

public sealed class AiFileWriter
{
    private readonly AiServerAccessPolicy _accessPolicy;
    private readonly AiActionGate _actionGate;
    private readonly RemoteFileEditor _editor;
    private readonly RemoteFileSaveService _saveService;

    public AiFileWriter(AiServerAccessPolicy accessPolicy, AiActionGate actionGate, RemoteFileEditor editor, RemoteFileSaveService saveService)
    {
        _accessPolicy = accessPolicy;
        _actionGate = actionGate;
        _editor = editor;
        _saveService = saveService;
    }

    public async Task<AiBridgeResult<AiFileWriteResult>> WriteAsync(AiFileWrite write, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(write.Path) || write.Content is null)
        {
            var invalid = AiBridgeError.Invalid("Pass the absolute file path and the new content.");
            return AiBridgeResult<AiFileWriteResult>.Failure(invalid);
        }

        var access = await _accessPolicy.CheckWritableAsync(write.ConnectionId, cancellationToken);
        if (!access.IsSuccess)
        {
            return AiBridgeResult<AiFileWriteResult>.Failure(access.Error);
        }

        var current = await ReadCurrentAsync(write, cancellationToken);
        if (!current.IsSuccess)
        {
            return AiBridgeResult<AiFileWriteResult>.Failure(current.Error);
        }

        var existing = current.Value;
        var before = existing?.Content.Text ?? string.Empty;
        var fileChange = new AiFileChange(write.Path, before, write.Content);
        var summary = existing is null ? $"Create {write.Path}" : $"Edit {write.Path}";
        var serverName = access.Value.Details.Name;
        var action = new AiActionDescription(write.ConnectionId, serverName, AiActionKind.FileWrite, summary) { FileChange = fileChange };
        var rejection = await _actionGate.AuthorizeAsync(action, cancellationToken);
        if (rejection is not null)
        {
            return AiBridgeResult<AiFileWriteResult>.Failure(rejection);
        }

        return await SaveAsync(write, existing, cancellationToken);
    }

    private async Task<AiBridgeResult<RemoteDocument?>> ReadCurrentAsync(AiFileWrite write, CancellationToken cancellationToken)
    {
        var opened = await _editor.OpenAsync(write.ConnectionId, write.Path, cancellationToken);
        if (!opened.IsSuccess)
        {
            if (opened.Error.Code == EditorErrorCode.NotFound && write.ExpectedSha256 is null)
            {
                return AiBridgeResult<RemoteDocument?>.Success(null);
            }

            var failure = AiBridgeErrorMapper.FromEditor(opened.Error);
            return AiBridgeResult<RemoteDocument?>.Failure(failure);
        }

        var document = opened.Value;
        if (write.ExpectedSha256 is null)
        {
            var exists = new AiBridgeError(AiBridgeErrorCode.Conflict, $"{document.Path} already exists. Read it first and pass its sha256 as expectedSha256.");
            return AiBridgeResult<RemoteDocument?>.Failure(exists);
        }

        if (!string.Equals(document.Version.Sha256, write.ExpectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            var changed = new AiBridgeError(AiBridgeErrorCode.Conflict, $"{document.Path} changed since you read it. Its sha256 is now {document.Version.Sha256}. Read it again.");
            return AiBridgeResult<RemoteDocument?>.Failure(changed);
        }

        return AiBridgeResult<RemoteDocument?>.Success(document);
    }

    private async Task<AiBridgeResult<AiFileWriteResult>> SaveAsync(AiFileWrite write, RemoteDocument? existing, CancellationToken cancellationToken)
    {
        var hasByteOrderMark = existing?.Content.HasByteOrderMark ?? false;
        var content = new TextContent(write.Content!, hasByteOrderMark);
        var request = new SaveDocumentRequest(write.Path, content, existing?.Version);
        var saved = await _saveService.SaveAsync(write.ConnectionId, request, cancellationToken);
        if (!saved.IsSuccess)
        {
            var failure = AiBridgeErrorMapper.FromEditor(saved.Error);
            return AiBridgeResult<AiFileWriteResult>.Failure(failure);
        }

        var result = saved.Value.Result;
        if (!result.IsSaved)
        {
            var conflict = new AiBridgeError(AiBridgeErrorCode.Conflict, result.Conflict.Reason);
            return AiBridgeResult<AiFileWriteResult>.Failure(conflict);
        }

        var path = existing?.Path ?? write.Path!;
        var backupName = result.Saved.Backup?.Name;
        var written = new AiFileWriteResult(path, result.Saved.Version.Sha256, backupName);
        return AiBridgeResult<AiFileWriteResult>.Success(written);
    }
}
