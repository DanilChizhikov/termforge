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
    RemotePathExists,
    RemoteNotADirectory,
    DirectoryNotEmpty,
    PermissionDenied,
    SessionClosed,
    OperationFailed,
    Cancelled,
}
