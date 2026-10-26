namespace DTech.Core.Docker;

public enum DockerErrorCode
{
    InvalidRequest,
    ConnectionNotFound,
    CredentialsUnavailable,
    ConnectionFailed,
    DockerMissing,
    PermissionDenied,
    DaemonUnavailable,
    ContainerNotFound,
    ConfirmationRequired,
    CommandFailed,
}
