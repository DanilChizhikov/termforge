namespace DTech.Core.SavedConnections;

public enum ConnectionTestState
{
    Connected,
    AuthenticationFailed,
    Unreachable,
    Timeout,
    HostKeyRejected,
    CredentialsUnavailable,
    Failed,
}
