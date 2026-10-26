using DTech.Core.Terminal.Sessions;

namespace DTech.Entrypoint.Terminals;

internal sealed record TerminalSessionResponse(string Id, Guid ConnectionId, string ConnectionName, int SessionNumber, DateTime OpenedAt)
{
    public static TerminalSessionResponse From(TerminalSessionDescriptor descriptor)
    {
        return new TerminalSessionResponse(descriptor.Id.Value, descriptor.ConnectionId, descriptor.ConnectionName, descriptor.SessionNumber, descriptor.OpenedAt);
    }
}
