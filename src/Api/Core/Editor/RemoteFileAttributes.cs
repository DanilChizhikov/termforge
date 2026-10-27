using DTech.Core.Files;

namespace DTech.Core.Editor;

public sealed record RemoteFileAttributes(RemoteFileKind Kind, long Size, DateTime LastWriteTimeUtc)
{
    public int Mode { get; init; }

    public int UserId { get; init; }

    public int GroupId { get; init; }
}
