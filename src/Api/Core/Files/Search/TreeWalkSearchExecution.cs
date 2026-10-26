using System.Runtime.CompilerServices;

namespace DTech.Core.Files.Search;

public sealed class TreeWalkSearchExecution(FileTreeSearcher searcher, IFileTreeReader reader, FileSearchQuery query) : IFileSearchExecution
{
    public string? Notice { get; init; }

    public async IAsyncEnumerable<FileSearchEvent> ExecuteAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (Notice is not null)
        {
            yield return new FileSearchNotice(Notice);
        }

        await foreach (var searchEvent in searcher.SearchAsync(reader, query, cancellationToken))
        {
            yield return searchEvent;
        }
    }

    public ValueTask DisposeAsync()
    {
        return reader.DisposeAsync();
    }
}
