namespace DTech.Core.Editor;

public readonly record struct RollbackRequest(RemoteFileLocation Location, string? BackupName, RemoteFileVersion? ExpectedVersion);
