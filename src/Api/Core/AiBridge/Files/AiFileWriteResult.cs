namespace DTech.Core.AiBridge.Files;

public sealed record AiFileWriteResult(string Path, string Sha256, string? BackupName);
