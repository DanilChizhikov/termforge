namespace DTech.Core.Terminal.Sessions;

public sealed record TerminalLaunchRequest(Guid ConnectionId, string OwnerKey, TerminalSize InitialSize);
