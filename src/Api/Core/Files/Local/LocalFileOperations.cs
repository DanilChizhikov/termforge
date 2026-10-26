namespace DTech.Core.Files.Local;

public sealed class LocalFileOperations(LocalPathResolver pathResolver)
{
    public FileResult CreateDirectory(CreateDirectoryRequest request)
    {
        var nameError = FileNameValidator.Validate(request.Name);
        if (nameError is not null)
        {
            return FileResult.Failure(nameError);
        }

        var parent = pathResolver.Resolve(request.ParentPath);
        if (!parent.IsSuccess)
        {
            return FileResult.Failure(parent.Error);
        }

        var target = Path.Combine(parent.Value, request.Name);
        if (LocalFileInfo.Open(target) is not null)
        {
            var exists = FileError.AlreadyExists(target);
            return FileResult.Failure(exists);
        }

        return Execute(() => Directory.CreateDirectory(target), target);
    }

    public FileResult Rename(RenameRequest request)
    {
        var nameError = FileNameValidator.Validate(request.NewName);
        if (nameError is not null)
        {
            return FileResult.Failure(nameError);
        }

        var source = ResolveExisting(request.Path);
        if (!source.IsSuccess)
        {
            return FileResult.Failure(source.Error);
        }

        var info = source.Value;
        var parentPath = Path.GetDirectoryName(info.FullName);
        if (parentPath is null)
        {
            var root = FileError.Invalid("A filesystem root cannot be renamed.");
            return FileResult.Failure(root);
        }

        var target = Path.Combine(parentPath, request.NewName);
        if (LocalFileInfo.Open(target) is not null)
        {
            var exists = FileError.AlreadyExists(target);
            return FileResult.Failure(exists);
        }

        return Execute(() => Move(info, target), info.FullName);
    }

    public FileResult Delete(DeleteRequest request)
    {
        if (!request.Confirmed)
        {
            var confirmation = FileError.ConfirmationRequired(request.Path);
            return FileResult.Failure(confirmation);
        }

        var source = ResolveExisting(request.Path);
        if (!source.IsSuccess)
        {
            return FileResult.Failure(source.Error);
        }

        var info = source.Value;
        if (Path.GetDirectoryName(info.FullName) is null)
        {
            var root = FileError.Invalid("A filesystem root cannot be deleted.");
            return FileResult.Failure(root);
        }

        return DeleteExisting(info);
    }

    private FileResult DeleteExisting(FileSystemInfo info)
    {
        var isPlainDirectory = info is DirectoryInfo && info.LinkTarget is null;
        if (isPlainDirectory && HasChildren(info.FullName))
        {
            var notEmpty = FileError.NotEmpty(info.FullName);
            return FileResult.Failure(notEmpty);
        }

        return Execute(() => DeleteEntry(info), info.FullName);
    }

    private static void DeleteEntry(FileSystemInfo info)
    {
        var isDirectoryLinkOnWindows = OperatingSystem.IsWindows() && info is DirectoryInfo;
        if (info.LinkTarget is not null && !isDirectoryLinkOnWindows)
        {
            File.Delete(info.FullName);
            return;
        }

        info.Delete();
    }

    private FileResult<FileSystemInfo> ResolveExisting(string path)
    {
        var resolved = pathResolver.Resolve(path);
        if (!resolved.IsSuccess)
        {
            return FileResult<FileSystemInfo>.Failure(resolved.Error);
        }

        var info = LocalFileInfo.Open(resolved.Value);
        if (info is null)
        {
            var missing = FileError.NotFound(resolved.Value);
            return FileResult<FileSystemInfo>.Failure(missing);
        }

        return FileResult<FileSystemInfo>.Success(info);
    }

    private static bool HasChildren(string directoryPath)
    {
        try
        {
            var directory = new DirectoryInfo(directoryPath);
            return directory.EnumerateFileSystemInfos().Any();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void Move(FileSystemInfo info, string target)
    {
        var isPlainDirectory = info is DirectoryInfo && info.LinkTarget is null;
        if (isPlainDirectory)
        {
            Directory.Move(info.FullName, target);
            return;
        }

        File.Move(info.FullName, target);
    }

    private static FileResult Execute(Action action, string path)
    {
        try
        {
            action();
            return FileResult.Success();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            var error = LocalIoErrors.Map(exception, path);
            return FileResult.Failure(error);
        }
    }
}
