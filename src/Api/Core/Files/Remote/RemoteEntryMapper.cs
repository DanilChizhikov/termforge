namespace DTech.Core.Files.Remote;

public static class RemoteEntryMapper
{
    public static FileEntry Map(RemoteFileEntry entry)
    {
        var kind = MapKind(entry.Kind);
        return new FileEntry(entry.Name, entry.FullPath, kind)
        {
            Size = entry.Size,
            ModifiedUtc = entry.LastWriteTimeUtc,
            Permissions = entry.Permissions,
            LinkTargetKind = entry.LinkTargetKind is null ? null : MapKind(entry.LinkTargetKind.Value),
            IsBrokenLink = entry.IsBrokenLink,
        };
    }

    private static FileEntryKind MapKind(RemoteFileKind kind)
    {
        return kind switch
        {
            RemoteFileKind.File => FileEntryKind.File,
            RemoteFileKind.Directory => FileEntryKind.Directory,
            RemoteFileKind.SymbolicLink => FileEntryKind.SymbolicLink,
            _ => FileEntryKind.Other,
        };
    }
}
