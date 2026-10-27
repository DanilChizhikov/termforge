using DTech.Core.Connections;

namespace DTech.Core.Files;

public static class FileErrorMapper
{
    public static FileError FromSsh(SshError error, string path)
    {
        return error.Kind switch
        {
            SshErrorKind.RemotePathNotFound => FileError.NotFound(path),
            SshErrorKind.PermissionDenied => FileError.PermissionDenied(path),
            SshErrorKind.RemotePathExists => FileError.AlreadyExists(path),
            SshErrorKind.DirectoryNotEmpty => FileError.NotEmpty(path),
            SshErrorKind.RemoteNotADirectory => FileError.NotADirectory(path),
            SshErrorKind.Cancelled => new FileError(FileErrorCode.Cancelled, error.Message),
            SshErrorKind.OperationFailed => new FileError(FileErrorCode.Failed, error.Message),
            SshErrorKind.ChannelFailed => new FileError(FileErrorCode.Failed, error.Message),
            _ => new FileError(FileErrorCode.ConnectionFailed, error.Message),
        };
    }
}
