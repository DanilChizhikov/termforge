namespace DTech.Core.Editor;

public sealed class BackupCatalog(AuxiliaryFileNames names)
{
    public async Task<EditorResult<IReadOnlyList<FileBackup>>> ListAsync(IRemoteEditorFileClient client, RemoteFileLocation location, CancellationToken cancellationToken)
    {
        var listed = await client.ListNamesAsync(location.Directory, cancellationToken);
        if (!listed.IsSuccess)
        {
            var failure = EditorErrorMapper.FromSsh(listed.Error!, location.Directory);
            return EditorResult<IReadOnlyList<FileBackup>>.Failure(failure);
        }

        var backups = new List<FileBackup>();
        var candidates = listed.Value;
        for (var i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            var createdAtUtc = names.ParseBackup(location.Name, candidate);
            if (createdAtUtc is not null)
            {
                var path = location.Sibling(candidate);
                var backup = new FileBackup(candidate, path, createdAtUtc.Value);
                backups.Add(backup);
            }
        }

        backups.Sort(static (left, right) => right.CreatedAtUtc.CompareTo(left.CreatedAtUtc));
        return EditorResult<IReadOnlyList<FileBackup>>.Success(backups);
    }

    public EditorResult<FileBackup> Resolve(RemoteFileLocation location, string? backupName)
    {
        var createdAtUtc = backupName is null ? null : names.ParseBackup(location.Name, backupName);
        if (createdAtUtc is null)
        {
            var invalid = EditorError.Invalid($"'{backupName}' is not a backup of {location.Name}.");
            return EditorResult<FileBackup>.Failure(invalid);
        }

        var path = location.Sibling(backupName!);
        var backup = new FileBackup(backupName!, path, createdAtUtc.Value);
        return EditorResult<FileBackup>.Success(backup);
    }
}
