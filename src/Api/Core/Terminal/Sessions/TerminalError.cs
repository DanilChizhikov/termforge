using DTech.Core.Connections;

namespace DTech.Core.Terminal.Sessions;

public sealed record TerminalError(TerminalErrorCode Code, string Message)
{
    public SshErrorKind? SshErrorKind { get; init; }

    public static TerminalError FromSsh(SshError error)
    {
        return new TerminalError(TerminalErrorCode.ConnectionFailed, error.Message)
        {
            SshErrorKind = error.Kind,
        };
    }
}
