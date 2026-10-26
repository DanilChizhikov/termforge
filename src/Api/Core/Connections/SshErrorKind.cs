namespace DTech.Core.Connections;

public enum SshErrorKind
{
    ConnectionFailed,
    HostKeyRejected,
    HostKeyChanged,
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
