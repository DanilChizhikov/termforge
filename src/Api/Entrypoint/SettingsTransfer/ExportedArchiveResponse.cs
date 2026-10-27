namespace DTech.Entrypoint.SettingsTransfer;

public sealed record ExportedArchiveResponse(string FileName, string ContentBase64, int SecretCount);
