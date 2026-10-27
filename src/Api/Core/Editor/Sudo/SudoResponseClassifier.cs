namespace DTech.Core.Editor.Sudo;

public sealed class SudoResponseClassifier
{
    private const int CommandNotFoundExitCode = 127;

    private static readonly string[] NotInstalledMarkers = ["sudo: not found", "sudo: command not found", "sudo: No such file or directory"];
    private static readonly string[] IncorrectMarkers = ["incorrect password", "Sorry, try again"];
    private static readonly string[] RequiredMarkers = ["a password is required", "no password was provided"];
    private static readonly string[] NotPermittedMarkers = ["not in the sudoers", "may not run sudo", "is not allowed to", "not allowed to execute"];
    private static readonly string[] TerminalMarkers = ["must have a tty", "a terminal is required"];

    public SudoResponse Classify(RemoteShellCommandResult result)
    {
        if (result.Succeeded)
        {
            return SudoResponse.Granted;
        }

        var standardError = result.StandardError;
        if (result.ExitCode == CommandNotFoundExitCode || ShellCommandFailures.ContainsAny(standardError, NotInstalledMarkers))
        {
            return SudoResponse.NotInstalled;
        }

        if (ShellCommandFailures.ContainsAny(standardError, IncorrectMarkers))
        {
            return SudoResponse.PasswordIncorrect;
        }

        if (ShellCommandFailures.ContainsAny(standardError, NotPermittedMarkers))
        {
            return SudoResponse.NotPermitted;
        }

        if (ShellCommandFailures.ContainsAny(standardError, RequiredMarkers))
        {
            return SudoResponse.PasswordRequired;
        }

        return ShellCommandFailures.ContainsAny(standardError, TerminalMarkers) ? SudoResponse.TerminalRequired : SudoResponse.Failed;
    }
}
