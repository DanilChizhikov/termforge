using DTech.Core.Safety;

namespace DTech.Core.Files;

public sealed record FileError(FileErrorCode Code, string Message)
{
    public static FileError Invalid(string message)
    {
        return new FileError(FileErrorCode.Invalid, message);
    }

    public static FileError NotFound(string path)
    {
        return new FileError(FileErrorCode.NotFound, $"Path does not exist: {path}");
    }

    public static FileError PermissionDenied(string path)
    {
        return new FileError(FileErrorCode.PermissionDenied, $"Permission denied: {path}");
    }

    public static FileError AlreadyExists(string path)
    {
        return new FileError(FileErrorCode.AlreadyExists, $"Path already exists: {path}");
    }

    public static FileError NotEmpty(string path)
    {
        return new FileError(FileErrorCode.NotEmpty, $"Directory is not empty: {path}");
    }

    public static FileError NotADirectory(string path)
    {
        return new FileError(FileErrorCode.NotADirectory, $"Not a directory: {path}");
    }

    public static FileError ReadOnlyServer()
    {
        return new FileError(FileErrorCode.ReadOnlyServer, ServerWriteGate.ReadOnlyMessage);
    }

    public static FileError OverwriteConfirmationRequired(string targetDirectory)
    {
        return new FileError(FileErrorCode.ConfirmationRequired, $"Overwriting files in {targetDirectory} requires explicit confirmation.");
    }

    public static FileError ConfirmationRequired(string path)
    {
        return new FileError(FileErrorCode.ConfirmationRequired, $"Deleting {path} requires explicit confirmation.");
    }
}
