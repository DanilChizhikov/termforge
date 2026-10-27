namespace DTech.Core.Editor;

public sealed record SavedFile(RemoteFileVersion Version, FileBackup? Backup, SaveStrategy Strategy, IReadOnlyList<string> Warnings);
