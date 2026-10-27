namespace DTech.Core.Editor;

public readonly record struct RollbackDocumentRequest(string? Path, string? BackupName, RemoteFileVersion? ExpectedVersion)
{
    public string? ConfirmationToken { get; init; }
}
