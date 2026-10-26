namespace DTech.Core.Editor;

public readonly record struct ReplaceRequest(RemoteFileLocation Location, byte[] Content, RemoteFileAttributes? Current);
