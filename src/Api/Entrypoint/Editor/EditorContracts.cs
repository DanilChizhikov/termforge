using DTech.Core.Editor;
using DTech.Core.Files;

namespace DTech.Entrypoint.Editor;

public sealed record FileVersionContract(DateTime ModifiedUtc, long Size, string Sha256)
{
    public static FileVersionContract From(RemoteFileVersion version)
    {
        return new FileVersionContract(version.ModifiedUtc, version.Size, version.Sha256);
    }

    public RemoteFileVersion ToVersion()
    {
        var modifiedUtc = ModifiedUtc.Kind == DateTimeKind.Utc ? ModifiedUtc : ModifiedUtc.ToUniversalTime();
        return new RemoteFileVersion(modifiedUtc, Size, Sha256 ?? string.Empty);
    }
}

public sealed record EditorDocumentResponse(string Path, string Content, bool HasByteOrderMark, FileVersionContract Version, string Permissions)
{
    public int OwnerId { get; init; }

    public static EditorDocumentResponse From(RemoteDocument document)
    {
        var version = FileVersionContract.From(document.Version);
        var permissions = PermissionFormatter.Format((UnixFileMode)document.Mode);
        return new EditorDocumentResponse(document.Path, document.Content.Text, document.Content.HasByteOrderMark, version, permissions)
        {
            OwnerId = document.OwnerId,
        };
    }
}

public sealed record FileBackupResponse(string Name, DateTime CreatedAtUtc)
{
    public static FileBackupResponse From(FileBackup backup)
    {
        return new FileBackupResponse(backup.Name, backup.CreatedAtUtc);
    }
}

public sealed record SaveFileBody(string? Path, string? Content, bool HasByteOrderMark, FileVersionContract? ExpectedVersion);

public sealed record RollbackBody(string? Path, string? BackupName, FileVersionContract? ExpectedVersion);

public sealed record SavedFileResponse(FileVersionContract Version, string? BackupName, string Strategy, IReadOnlyList<string> Warnings, Guid OperationId)
{
    public EditorDocumentResponse? Document { get; init; }

    public static SavedFileResponse From(JournaledSave journaled)
    {
        var saved = journaled.Result.Saved!;
        var version = FileVersionContract.From(saved.Version);
        var strategy = saved.Strategy.ToString();
        var document = journaled.Document is null ? null : EditorDocumentResponse.From(journaled.Document);
        return new SavedFileResponse(version, saved.Backup?.Name, strategy, saved.Warnings, journaled.OperationId)
        {
            Document = document,
        };
    }
}

public sealed record ConflictResponse(string Code, string Detail, FileVersionContract? CurrentVersion, string? CurrentContent, bool CurrentHasByteOrderMark, Guid OperationId)
{
    public const string ConflictCode = "Conflict";

    public static ConflictResponse From(JournaledSave journaled)
    {
        var conflict = journaled.Result.Conflict!;
        var version = conflict.CurrentVersion is null ? null : FileVersionContract.From(conflict.CurrentVersion);
        var content = conflict.CurrentContent?.Text;
        var hasByteOrderMark = conflict.CurrentContent?.HasByteOrderMark ?? false;
        return new ConflictResponse(ConflictCode, conflict.Reason, version, content, hasByteOrderMark, journaled.OperationId);
    }
}
