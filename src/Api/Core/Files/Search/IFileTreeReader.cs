namespace DTech.Core.Files.Search;

public interface IFileTreeReader : IAsyncDisposable
{
    FileSearchMethod Method { get; }

    Task<FileResult<IReadOnlyList<FileEntry>>> ListAsync(string directoryPath, CancellationToken cancellationToken);
}
