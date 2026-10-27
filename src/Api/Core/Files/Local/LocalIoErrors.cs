namespace DTech.Core.Files.Local;

public static class LocalIoErrors
{
    public static FileError Map(Exception exception, string path)
    {
        return exception switch
        {
            UnauthorizedAccessException => FileError.PermissionDenied(path),
            DirectoryNotFoundException => FileError.NotFound(path),
            FileNotFoundException => FileError.NotFound(path),
            PathTooLongException => FileError.Invalid($"Path is too long: {path}"),
            _ => new FileError(FileErrorCode.Failed, $"{path}: {exception.Message}"),
        };
    }
}
