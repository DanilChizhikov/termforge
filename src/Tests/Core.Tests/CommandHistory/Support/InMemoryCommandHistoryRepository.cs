using DTech.Core.CommandHistory;

namespace DTech.Core.Tests.CommandHistory.Support;

internal sealed class InMemoryCommandHistoryRepository : ICommandHistoryRepository
{
    private readonly List<CommandHistoryEntry> _entries = [];

    public IReadOnlyList<CommandHistoryEntry> Snapshot => _entries.ToList();

    public Task AddAsync(CommandHistoryEntry entry, CancellationToken cancellationToken)
    {
        _entries.Add(entry);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<CommandHistoryEntry>> ListAsync(CommandHistoryQuery query, CancellationToken cancellationToken)
    {
        IReadOnlyList<CommandHistoryEntry> result = _entries
            .Where(entry => entry.ConnectionId == query.ConnectionId)
            .Where(entry => query.Search is null || entry.Command.Contains(query.Search, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(entry => entry.IsPinned)
            .ThenByDescending(entry => entry.ExecutedAt)
            .Take(query.Limit)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<bool> SetPinnedAsync(Guid id, bool isPinned, CancellationToken cancellationToken)
    {
        var index = _entries.FindIndex(entry => entry.Id == id);
        if (index < 0)
        {
            return Task.FromResult(false);
        }

        _entries[index] = _entries[index] with { IsPinned = isPinned };
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        return Task.FromResult(_entries.RemoveAll(entry => entry.Id == id) > 0);
    }

    public Task<int> ClearUnpinnedAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        return Task.FromResult(_entries.RemoveAll(entry => entry.ConnectionId == connectionId && !entry.IsPinned));
    }

    public Task<int> PruneUnpinnedAsync(Guid connectionId, int keepNewest, CancellationToken cancellationToken)
    {
        var surplus = _entries
            .Where(entry => entry.ConnectionId == connectionId && !entry.IsPinned)
            .OrderByDescending(entry => entry.ExecutedAt)
            .Skip(keepNewest)
            .Select(entry => entry.Id)
            .ToList();
        return Task.FromResult(_entries.RemoveAll(entry => surplus.Contains(entry.Id)));
    }
}
