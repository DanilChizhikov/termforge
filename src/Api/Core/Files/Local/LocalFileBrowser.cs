namespace DTech.Core.Files.Local;

public sealed class LocalFileBrowser(LocalPathResolver pathResolver, LocalRootsProvider rootsProvider, LocalEntryFactory entryFactory)
{
    private static readonly EnumerationOptions ListingOptions = new()
    {
        AttributesToSkip = 0,
        IgnoreInaccessible = false,
        RecurseSubdirectories = false,
    };

    public IReadOnlyList<LocalRoot> GetRoots()
    {
        return rootsProvider.GetRoots();
    }

    public FileResult<DirectoryListing> List(string? requestedPath)
    {
        var rawPath = string.IsNullOrWhiteSpace(requestedPath) ? rootsProvider.GetHomeDirectory() : requestedPath;
        var resolved = pathResolver.Resolve(rawPath);
        if (!resolved.IsSuccess)
        {
            return FileResult<DirectoryListing>.Failure(resolved.Error);
        }

        var path = resolved.Value;
        var rejection = FindListingRejection(path);
        if (rejection is not null)
        {
            return FileResult<DirectoryListing>.Failure(rejection);
        }

        return ReadListing(path);
    }

    public FileResult<FileEntry> Stat(string? requestedPath)
    {
        var resolved = pathResolver.Resolve(requestedPath);
        if (!resolved.IsSuccess)
        {
            return FileResult<FileEntry>.Failure(resolved.Error);
        }

        var path = resolved.Value;
        var info = LocalFileInfo.Open(path);
        if (info is null)
        {
            var missing = FileError.NotFound(path);
            return FileResult<FileEntry>.Failure(missing);
        }

        var entry = entryFactory.Create(info);
        return FileResult<FileEntry>.Success(entry);
    }

    private static FileError? FindListingRejection(string path)
    {
        if (Directory.Exists(path))
        {
            return null;
        }

        var info = LocalFileInfo.Open(path);
        if (info is null)
        {
            return FileError.NotFound(path);
        }

        if (info.LinkTarget is not null)
        {
            return FileError.Invalid($"Symbolic link is broken or circular: {path}");
        }

        return FileError.NotADirectory(path);
    }

    private FileResult<DirectoryListing> ReadListing(string path)
    {
        try
        {
            var directory = new DirectoryInfo(path);
            var entries = new List<FileEntry>();
            foreach (var info in directory.EnumerateFileSystemInfos("*", ListingOptions))
            {
                var entry = entryFactory.Create(info);
                entries.Add(entry);
            }

            var parent = Directory.GetParent(path)?.FullName;
            var listing = new DirectoryListing(path, parent, entries);
            return FileResult<DirectoryListing>.Success(listing);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            var error = LocalIoErrors.Map(exception, path);
            return FileResult<DirectoryListing>.Failure(error);
        }
    }
}
