namespace DTech.Core.CommandHistory;

public interface ICommandHistoryRepository
{
    Task AddAsync(CommandHistoryEntry entry, CancellationToken cancellationToken);

    Task<IReadOnlyList<CommandHistoryEntry>> ListAsync(CommandHistoryQuery query, CancellationToken cancellationToken);

    Task<bool> SetPinnedAsync(Guid id, bool isPinned, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);

    Task<int> ClearUnpinnedAsync(Guid connectionId, CancellationToken cancellationToken);

    Task<int> PruneUnpinnedAsync(Guid connectionId, int keepNewest, CancellationToken cancellationToken);
}
