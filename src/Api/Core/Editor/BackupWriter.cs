namespace DTech.Core.Editor;

public sealed class BackupWriter(AuxiliaryFileNames names, TimeProvider timeProvider)
{
    private const string NotChanged = "The backup could not be created, so the file was not changed.";

    public async Task<EditorResult<FileBackup>> WriteAsync(IRemoteEditorFileClient client, BackupSource source, CancellationToken cancellationToken)
    {
        var createdAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        var name = names.Backup(source.Location.Name, createdAtUtc);
        var path = source.Location.Sibling(name);
        var creation = new RemoteFileCreation(path, source.Snapshot.Content, source.Snapshot.Attributes.Mode);
        var created = await client.CreateFileAsync(creation, cancellationToken);
        if (!created.IsSuccess)
        {
            var failure = EditorErrorMapper.FromSsh(created.Error!, path).Prefixed(NotChanged);
            return EditorResult<FileBackup>.Failure(failure);
        }

        var backup = new FileBackup(name, path, createdAtUtc);
        return EditorResult<FileBackup>.Success(backup);
    }
}
