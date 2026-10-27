namespace DTech.Core.SettingsTransfer.Export;

public readonly record struct SettingsDocumentHeader(Guid ExportId, DateTime ExportedAt, bool HasSecretsArchive);
