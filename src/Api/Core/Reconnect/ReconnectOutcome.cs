using DTech.Core.Terminal.Sessions;

namespace DTech.Core.Reconnect;

public sealed record ReconnectOutcome(ManagedTerminalSession? Session, TerminalError? Error, int Attempts)
{
    public bool IsSuccess => Session is not null;

    public static ReconnectOutcome Opened(ManagedTerminalSession session, int attempts)
    {
        return new ReconnectOutcome(session, null, attempts);
    }

    public static ReconnectOutcome Failed(TerminalError error, int attempts)
    {
        return new ReconnectOutcome(null, error, attempts);
    }
}
