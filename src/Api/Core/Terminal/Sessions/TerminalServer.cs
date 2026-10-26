using DTech.Core.Connections;

namespace DTech.Core.Terminal.Sessions;

public sealed record TerminalServer(Guid ConnectionId, string Name, SshConnectionTarget Target);
