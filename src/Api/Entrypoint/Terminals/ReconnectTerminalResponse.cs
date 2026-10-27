using DTech.Core.Terminal.Sessions;

namespace DTech.Entrypoint.Terminals;

internal sealed record ReconnectTerminalResponse(TerminalSessionResponse Session, int Attempts)
{
    public static ReconnectTerminalResponse From(TerminalSessionDescriptor descriptor, int attempts)
    {
        var session = TerminalSessionResponse.From(descriptor);
        return new ReconnectTerminalResponse(session, attempts);
    }
}
