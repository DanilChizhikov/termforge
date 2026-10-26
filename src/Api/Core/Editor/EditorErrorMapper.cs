using DTech.Core.Connections;
using DTech.Core.Files;

namespace DTech.Core.Editor;

public static class EditorErrorMapper
{
    public static EditorError FromSsh(SshError error, string path)
    {
        return error.Kind switch
        {
            SshErrorKind.RemotePathNotFound => EditorError.NotFound(path),
            SshErrorKind.PermissionDenied => new EditorError(EditorErrorCode.PermissionDenied, $"Permission denied: {path}"),
            SshErrorKind.Cancelled => new EditorError(EditorErrorCode.Cancelled, error.Message),
            SshErrorKind.OperationFailed => new EditorError(EditorErrorCode.Failed, error.Message),
            SshErrorKind.ChannelFailed => new EditorError(EditorErrorCode.Failed, error.Message),
            SshErrorKind.RemotePathExists => new EditorError(EditorErrorCode.Failed, error.Message),
            _ => new EditorError(EditorErrorCode.ConnectionFailed, error.Message),
        };
    }

    public static EditorError FromFile(FileError error)
    {
        var code = error.Code switch
        {
            FileErrorCode.NotFound => EditorErrorCode.NotFound,
            FileErrorCode.Invalid => EditorErrorCode.Invalid,
            FileErrorCode.PermissionDenied => EditorErrorCode.PermissionDenied,
            FileErrorCode.ConnectionFailed => EditorErrorCode.ConnectionFailed,
            FileErrorCode.Cancelled => EditorErrorCode.Cancelled,
            _ => EditorErrorCode.Failed,
        };
        return new EditorError(code, error.Message);
    }
}
