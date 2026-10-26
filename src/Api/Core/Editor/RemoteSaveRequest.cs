namespace DTech.Core.Editor;

public readonly record struct RemoteSaveRequest(RemoteFileLocation Location, byte[] Content, RemoteFileVersion? ExpectedVersion);
