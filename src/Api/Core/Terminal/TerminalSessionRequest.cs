using DTech.Core.Connections;

namespace DTech.Core.Terminal;

public sealed record TerminalSessionRequest(SshConnectionTarget Target, TerminalSize InitialSize, string TerminalType)
{
    public const string DefaultTerminalType = "xterm-256color";
}
