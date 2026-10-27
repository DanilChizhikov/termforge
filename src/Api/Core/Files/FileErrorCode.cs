namespace DTech.Core.Files;

public enum FileErrorCode
{
    Invalid,
    NotFound,
    PermissionDenied,
    AlreadyExists,
    NotEmpty,
    NotADirectory,
    ConfirmationRequired,
    ReadOnlyServer,
    ConnectionFailed,
    Cancelled,
    Failed,
}
