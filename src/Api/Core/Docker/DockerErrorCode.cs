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
    ImageNotFound,
    ImageInUse,
    ComposeProjectNotFound,
    ComposeMissing,
    ConfirmationRequired,
    ReadOnlyServer,
    CommandFailed,
}
