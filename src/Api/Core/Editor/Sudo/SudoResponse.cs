namespace DTech.Core.Editor.Sudo;

public enum SudoResponse
{
    Granted,
    PasswordRequired,
    PasswordIncorrect,
    NotPermitted,
    NotInstalled,
    TerminalRequired,
    Failed,
}
