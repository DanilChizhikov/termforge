using DTech.Core.Terminal.Sessions;

namespace DTech.Entrypoint.Lifecycle;

internal sealed record ActiveTerminalResponse(Guid ConnectionId, string Server, int SessionNumber, DateTime OpenedAt)
{
    public static ActiveTerminalResponse From(TerminalSessionDescriptor descriptor)
    {
        return new ActiveTerminalResponse(descriptor.ConnectionId, descriptor.ConnectionName, descriptor.SessionNumber, descriptor.OpenedAt);
    }
}
