namespace DTech.Core.Files.Search;

public interface IFileSearchExecution : IAsyncDisposable
{
    IAsyncEnumerable<FileSearchEvent> ExecuteAsync(CancellationToken cancellationToken);
}
