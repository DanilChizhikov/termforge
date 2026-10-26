namespace DTech.Core.Editor;

public readonly record struct BackupSource(RemoteFileLocation Location, RemoteFileSnapshot Snapshot);
