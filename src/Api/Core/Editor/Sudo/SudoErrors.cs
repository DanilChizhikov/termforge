namespace DTech.Core.Editor.Sudo;

public static class SudoErrors
{
    public const string PasswordRequiredMessage = "sudo needs a password for this user. Enter the sudo password to continue.";
    public const string PasswordIncorrectMessage = "The sudo password is incorrect.";
    public const string NotInstalledMessage = "sudo is not installed on this server.";
    public const string NotPermittedMessage = "This user is not allowed to use sudo on this server.";
    public const string TerminalRequiredMessage = "sudo on this server requires a terminal (requiretty), so files cannot be edited with sudo.";
    public const string InvalidPasswordMessage = "The sudo password cannot contain line breaks or NUL characters.";

    public static EditorError? FromResponse(SudoResponse response, RemoteShellCommandResult result)
    {
        return response switch
        {
            SudoResponse.Granted => null,
            SudoResponse.PasswordRequired => new EditorError(EditorErrorCode.SudoPasswordRequired, PasswordRequiredMessage),
            SudoResponse.PasswordIncorrect => new EditorError(EditorErrorCode.SudoPasswordIncorrect, PasswordIncorrectMessage),
            SudoResponse.NotInstalled => new EditorError(EditorErrorCode.SudoUnavailable, NotInstalledMessage),
            SudoResponse.NotPermitted => new EditorError(EditorErrorCode.SudoUnavailable, NotPermittedMessage),
            SudoResponse.TerminalRequired => new EditorError(EditorErrorCode.SudoUnavailable, TerminalRequiredMessage),
            _ => Failed(result),
        };
    }

    private static EditorError Failed(RemoteShellCommandResult result)
    {
        var detail = ShellCommandFailures.FirstLine(result.StandardError);
        var message = detail.Length == 0 ? "sudo failed." : $"sudo failed: {detail}";
        return new EditorError(EditorErrorCode.Failed, message);
    }
}
