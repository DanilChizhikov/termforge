namespace DTech.Core.Terminal.Sessions;

public sealed record TerminalSessionDescriptor(TerminalSessionId Id, Guid ConnectionId, string ConnectionName, int SessionNumber, DateTime OpenedAt);
