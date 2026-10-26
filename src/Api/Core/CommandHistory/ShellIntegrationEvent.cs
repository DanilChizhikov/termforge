namespace DTech.Core.CommandHistory;

public enum ShellIntegrationEventKind
{
    CommandLine,
    CommandFinished,
}

public readonly record struct ShellIntegrationEvent(ShellIntegrationEventKind Kind, string? CommandLine, int? ExitCode)
{
    public static ShellIntegrationEvent ForCommandLine(string commandLine)
    {
        return new ShellIntegrationEvent(ShellIntegrationEventKind.CommandLine, commandLine, null);
    }

    public static ShellIntegrationEvent ForCommandFinished(int? exitCode)
    {
        return new ShellIntegrationEvent(ShellIntegrationEventKind.CommandFinished, null, exitCode);
    }
}
