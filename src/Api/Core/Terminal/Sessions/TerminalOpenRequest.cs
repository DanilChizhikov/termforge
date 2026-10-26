namespace DTech.Core.Terminal.Sessions;

public sealed record TerminalOpenRequest(string OwnerKey, TerminalServer Server, TerminalSize InitialSize);
