using System.Net.Sockets;
using DTech.Core.Connections;
using Renci.SshNet.Common;
using SshNet.Agent;

namespace DTech.Ssh.Connections;

internal static class SshExceptionMapper
{
    public static SshError Map(Exception exception, string operation)
    {
        var kind = MapKind(exception);
        return new SshError(kind, $"{operation} failed: {exception.Message}");
    }

    private static SshErrorKind MapKind(Exception exception)
    {
        return exception switch
        {
            OperationCanceledException => SshErrorKind.Cancelled,
            SshAuthenticationException => SshErrorKind.AuthenticationFailed,
            SshPassPhraseNullOrEmptyException => SshErrorKind.PrivateKeyUnusable,
            SftpPathNotFoundException => SshErrorKind.RemotePathNotFound,
            SftpPermissionDeniedException => SshErrorKind.PermissionDenied,
            SshAgentException => SshErrorKind.AgentUnavailable,
            SocketException => SshErrorKind.ConnectionFailed,
            ProxyException => SshErrorKind.ConnectionFailed,
            SshOperationTimeoutException => SshErrorKind.ConnectionFailed,
            SshConnectionException => SshErrorKind.ConnectionFailed,
            ObjectDisposedException => SshErrorKind.SessionClosed,
            _ => SshErrorKind.OperationFailed,
        };
    }
}
