namespace DTech.Core.Editor;

public readonly record struct ConflictCheck(string Path, RemoteFileVersion? ExpectedVersion, RemoteFileSnapshot? Current);
