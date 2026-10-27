namespace DTech.Core.Editor;

public sealed record RemoteFileSnapshot(RemoteFileAttributes Attributes, byte[] Content, RemoteFileVersion Version);
