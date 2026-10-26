using DTech.Core.Connections;
using DTech.Core.Terminal.Sessions;

namespace DTech.Core.Reconnect;

public static class ReconnectErrorClassifier
{
    public static bool IsTransient(TerminalError error)
    {
        if (error.Code != TerminalErrorCode.ConnectionFailed)
        {
            return false;
        }

        return error.SshErrorKind is null or SshErrorKind.ConnectionFailed or SshErrorKind.ChannelFailed or SshErrorKind.SessionClosed or SshErrorKind.OperationFailed;
    }
}
