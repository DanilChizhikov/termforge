namespace DTech.Core.Editor.Sudo;

public sealed record SudoDocumentRequest(Guid ConnectionId, string? Path, SudoCredentials Credentials);

public sealed record SudoBackupRequest(Guid ConnectionId, BackupReference Reference, SudoCredentials Credentials);

public sealed record SudoSaveRequest(Guid ConnectionId, SaveDocumentRequest Document, SudoCredentials Credentials);

public sealed record SudoRollbackRequest(Guid ConnectionId, RollbackDocumentRequest Document, SudoCredentials Credentials);
