namespace DTech.Core.Editor;

public sealed class BackupPruner(BackupCatalog catalog, EditorLimits limits)
{
    public async Task<IReadOnlyList<string>> PruneAsync(IRemoteEditorFileClient client, RemoteFileLocation location, CancellationToken cancellationToken)
    {
        var warnings = new List<string>();
        var listed = await catalog.ListAsync(client, location, cancellationToken);
        if (!listed.IsSuccess)
        {
            warnings.Add($"Old backups could not be cleaned up: {listed.Error.Message}");
            return warnings;
        }

        var backups = listed.Value;
        for (var i = limits.MaxBackupsPerFile; i < backups.Count; i++)
        {
            var backup = backups[i];
            var deleted = await client.DeleteFileAsync(backup.Path, cancellationToken);
            if (!deleted.IsSuccess)
            {
                warnings.Add($"Old backup {backup.Name} could not be deleted: {deleted.Error!.Message}");
            }
        }

        return warnings;
    }
}
