namespace DTech.Core.Editor;

public sealed class RemoteFileSaver
{
    private const string VersionUnknown = "The file was saved, but its new version could not be read. Reopen the file before editing it again.";

    private readonly RemoteFileSnapshotReader _snapshots;
    private readonly ConflictDetector _conflicts;
    private readonly BackupWriter _backups;
    private readonly FileReplacer _replacer;
    private readonly BackupPruner _pruner;
    private readonly EditorLimits _limits;

    public RemoteFileSaver(
        RemoteFileSnapshotReader snapshots,
        ConflictDetector conflicts,
        BackupWriter backups,
        FileReplacer replacer,
        BackupPruner pruner,
        EditorLimits limits)
    {
        _snapshots = snapshots;
        _conflicts = conflicts;
        _backups = backups;
        _replacer = replacer;
        _pruner = pruner;
        _limits = limits;
    }

    public async Task<EditorResult<RemoteSaveResult>> SaveAsync(IRemoteEditorFileClient client, RemoteSaveRequest request, CancellationToken cancellationToken)
    {
        var location = request.Location;
        if (request.Content.LongLength > _limits.MaxFileBytes)
        {
            var tooLarge = EditorError.TooLarge(location.Path, _limits.MaxFileBytes);
            return EditorResult<RemoteSaveResult>.Failure(tooLarge);
        }

        var current = await _snapshots.ReadAsync(client, location.Path, cancellationToken);
        if (!current.IsSuccess)
        {
            return EditorResult<RemoteSaveResult>.Failure(current.Error);
        }

        var check = new ConflictCheck(location.Path, request.ExpectedVersion, current.Value);
        var conflict = _conflicts.Detect(check);
        if (conflict is not null)
        {
            var conflicted = RemoteSaveResult.Conflicted(conflict);
            return EditorResult<RemoteSaveResult>.Success(conflicted);
        }

        FileBackup? backup = null;
        if (current.Value is not null)
        {
            var source = new BackupSource(location, current.Value);
            var written = await _backups.WriteAsync(client, source, cancellationToken);
            if (!written.IsSuccess)
            {
                return EditorResult<RemoteSaveResult>.Failure(written.Error);
            }

            backup = written.Value;
        }

        var replaceRequest = new ReplaceRequest(location, request.Content, current.Value?.Attributes);
        var replaced = await _replacer.ReplaceAsync(client, replaceRequest, cancellationToken);
        if (!replaced.IsSuccess)
        {
            return EditorResult<RemoteSaveResult>.Failure(replaced.Error);
        }

        var pruneWarnings = await _pruner.PruneAsync(client, location, CancellationToken.None);
        var warnings = new List<string>(replaced.Value.Warnings);
        warnings.AddRange(pruneWarnings);
        var attributes = await client.GetAttributesAsync(location.Path, CancellationToken.None);
        if (!attributes.IsSuccess)
        {
            var unknown = new EditorError(EditorErrorCode.Failed, VersionUnknown);
            return EditorResult<RemoteSaveResult>.Failure(unknown);
        }

        var version = RemoteFileVersion.Of(attributes.Value, request.Content);
        var saved = new SavedFile(version, backup, replaced.Value.Strategy, warnings);
        var completed = RemoteSaveResult.Completed(saved);
        return EditorResult<RemoteSaveResult>.Success(completed);
    }
}
