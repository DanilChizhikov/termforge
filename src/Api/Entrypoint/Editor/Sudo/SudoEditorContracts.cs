using DTech.Core.Credentials;
using DTech.Core.Editor;
using DTech.Core.Editor.Sudo;

namespace DTech.Entrypoint.Editor.Sudo;

public sealed class SudoCredentialsContract
{
    public string? Password { get; init; }

    public bool RememberPassword { get; init; }

    public static SudoCredentials ToCredentials(SudoCredentialsContract? contract)
    {
        return contract is null ? SudoCredentials.None : new SudoCredentials(contract.Password, contract.RememberPassword);
    }

    public override string ToString()
    {
        return Secret.Redacted;
    }
}

public sealed record SudoOpenBody(string? Path, SudoCredentialsContract? Sudo);

public sealed record SudoSaveBody(string? Path, string? Content, bool HasByteOrderMark, FileVersionContract? ExpectedVersion, SudoCredentialsContract? Sudo);

public sealed record SudoReadBackupBody(string? Path, string? Name, SudoCredentialsContract? Sudo);

public sealed record SudoRollbackBody(string? Path, string? BackupName, FileVersionContract? ExpectedVersion, SudoCredentialsContract? Sudo);

public sealed record SudoStatusResponse(string Availability, bool HasSavedPassword, string Message)
{
    public static SudoStatusResponse From(SudoStatus status)
    {
        var availability = status.Availability.ToString();
        return new SudoStatusResponse(availability, status.HasSavedPassword, status.Message);
    }
}
