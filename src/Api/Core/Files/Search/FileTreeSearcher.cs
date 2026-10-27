using System.Runtime.CompilerServices;

namespace DTech.Core.Files.Search;

public sealed class FileTreeSearcher(TimeProvider timeProvider)
{
    public async IAsyncEnumerable<FileSearchEvent> SearchAsync(IFileTreeReader reader, FileSearchQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var limits = query.Limits;
        using var timeLimit = new CancellationTokenSource(limits.TimeLimit, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeLimit.Token);
        var deadline = new SearchDeadline(timeProvider, limits.TimeLimit, timeLimit);
        var tally = new FileSearchTally(reader.Method);
        var pending = new Queue<PendingDirectory>();
        var root = new PendingDirectory(query.RootPath, 0);
        pending.Enqueue(root);
        while (pending.Count > 0)
        {
            var stop = FindStopOutcome(deadline, cancellationToken);
            if (stop is not null)
            {
                yield return tally.Complete(stop.Value);
                yield break;
            }

            var directory = pending.Dequeue();
            var listed = await ReadAsync(reader, directory.Path, linked.Token);
            if (!listed.IsSuccess)
            {
                var interruption = FindStopOutcome(deadline, cancellationToken);
                if (interruption is not null)
                {
                    yield return tally.Complete(interruption.Value);
                    yield break;
                }

                if (directory.Depth == 0)
                {
                    yield return tally.Fail(listed.Error.Message);
                    yield break;
                }

                if (tally.RecordWarning())
                {
                    yield return new FileSearchWarning(directory.Path, listed.Error);
                }

                continue;
            }

            var entries = SortByName(listed.Value);
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (query.Pattern.IsMatch(entry.Name))
                {
                    if (tally.MatchCount >= limits.MaxResults)
                    {
                        yield return tally.Complete(FileSearchOutcome.ResultLimitReached);
                        yield break;
                    }

                    tally.RecordMatch();
                    yield return new FileSearchMatch(entry, directory.Path);
                }

                if (entry.Kind != FileEntryKind.Directory)
                {
                    continue;
                }

                var childDepth = directory.Depth + 1;
                if (childDepth < limits.MaxDepth)
                {
                    var child = new PendingDirectory(entry.Path, childDepth);
                    pending.Enqueue(child);
                }
                else
                {
                    tally.RecordDepthLimit();
                }
            }
        }

        yield return tally.Complete(FileSearchOutcome.Completed);
    }

    private static FileSearchOutcome? FindStopOutcome(SearchDeadline deadline, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return FileSearchOutcome.Cancelled;
        }

        return deadline.HasPassed() ? FileSearchOutcome.TimeLimitReached : null;
    }

    private static async Task<FileResult<IReadOnlyList<FileEntry>>> ReadAsync(IFileTreeReader reader, string path, CancellationToken cancellationToken)
    {
        try
        {
            return await reader.ListAsync(path, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            var cancelled = new FileError(FileErrorCode.Cancelled, $"Listing {path} was interrupted.");
            return FileResult<IReadOnlyList<FileEntry>>.Failure(cancelled);
        }
    }

    private static List<FileEntry> SortByName(IReadOnlyList<FileEntry> entries)
    {
        var sorted = new List<FileEntry>(entries);
        sorted.Sort(static (left, right) => string.CompareOrdinal(left.Name, right.Name));
        return sorted;
    }

    private readonly record struct PendingDirectory(string Path, int Depth);

    private sealed class SearchDeadline
    {
        private readonly TimeProvider _timeProvider;
        private readonly TimeSpan _limit;
        private readonly CancellationTokenSource _timer;
        private readonly long _startTimestamp;

        public SearchDeadline(TimeProvider timeProvider, TimeSpan limit, CancellationTokenSource timer)
        {
            _timeProvider = timeProvider;
            _limit = limit;
            _timer = timer;
            _startTimestamp = timeProvider.GetTimestamp();
        }

        public bool HasPassed()
        {
            if (_timer.IsCancellationRequested)
            {
                return true;
            }

            var elapsed = _timeProvider.GetElapsedTime(_startTimestamp);
            return elapsed >= _limit;
        }
    }
}
