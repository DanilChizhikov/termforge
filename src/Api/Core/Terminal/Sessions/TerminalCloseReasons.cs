namespace DTech.Core.Terminal.Sessions;

public static class TerminalCloseReasons
{
    public const string ClosedByUser = "The terminal was closed.";
    public const string BrowserDisconnected = "The browser disconnected from the terminal.";
    public const string RemoteShellExited = "The remote shell exited or the SSH connection was lost.";
    public const string ShuttingDown = "TermForge is shutting down.";
    public const string NotAttached = "The terminal was not attached in time and was closed.";
}
