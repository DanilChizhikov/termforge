namespace DTech.Core.Files;

public sealed record RemoteFileEntry(string Name, string FullPath, RemoteFileKind Kind)
{
    public long Size { get; init; }

    public DateTime LastWriteTimeUtc { get; init; }
}
