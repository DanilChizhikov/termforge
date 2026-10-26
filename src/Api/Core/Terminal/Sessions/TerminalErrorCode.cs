namespace DTech.Core.Terminal.Sessions;

public enum TerminalErrorCode
{
    InvalidRequest,
    NotFound,
    CredentialsUnavailable,
    SessionLimitReached,
    ShuttingDown,
    ConnectionFailed,
    ReconnectDisabled,
    ReconnectFailed,
}
