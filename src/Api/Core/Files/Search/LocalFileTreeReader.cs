using DTech.Core.Files.Local;

namespace DTech.Core.Files.Search;

public sealed class LocalFileTreeReader(LocalEntryFactory entryFactory) : IFileTreeReader
{
    private static readonly EnumerationOptions ListingOptions = new()
    {
        AttributesToSkip = 0,
        IgnoreInaccessible = false,
        RecurseSubdirectories = false,
    };

    public FileSearchMethod Method => FileSearchMethod.Local;

    public Task<FileResult<IReadOnlyList<FileEntry>>> ListAsync(string directoryPath, CancellationToken cancellationToken)
    {
        return Task.Run(() => List(directoryPath, cancellationToken), cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    private FileResult<IReadOnlyList<FileEntry>> List(string directoryPath, CancellationToken cancellationToken)
    {
        try
        {
            var directory = new DirectoryInfo(directoryPath);
            var entries = new List<FileEntry>();
            foreach (var info in directory.EnumerateFileSystemInfos("*", ListingOptions))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entry = entryFactory.Create(info);
                entries.Add(entry);
            }

            return FileResult<IReadOnlyList<FileEntry>>.Success(entries);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            var error = LocalIoErrors.Map(exception, directoryPath);
            return FileResult<IReadOnlyList<FileEntry>>.Failure(error);
        }
    }
}
