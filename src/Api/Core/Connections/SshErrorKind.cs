namespace DTech.Core.Connections;

public enum SshErrorKind
{
    ConnectionFailed,
    HostKeyRejected,
    AuthenticationFailed,
    PrivateKeyUnusable,
    AgentUnavailable,
    ChannelFailed,
    RemotePathNotFound,
    PermissionDenied,
    SessionClosed,
    OperationFailed,
    Cancelled,
}
