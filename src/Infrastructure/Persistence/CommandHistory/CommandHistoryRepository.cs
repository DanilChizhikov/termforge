using DTech.Core.CommandHistory;
using DTech.Persistence.Database;
using Microsoft.EntityFrameworkCore;

namespace DTech.Persistence.CommandHistory;

public sealed class CommandHistoryRepository(TermForgeDbContext context) : ICommandHistoryRepository
{
    private const string LikeEscape = "\\";

    public async Task AddAsync(CommandHistoryEntry entry, CancellationToken cancellationToken)
    {
        var entity = CommandHistoryMapper.ToEntity(entry);
        context.Set<CommandHistoryEntity>().Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        context.Entry(entity).State = EntityState.Detached;
    }

    public async Task<IReadOnlyList<CommandHistoryEntry>> ListAsync(CommandHistoryQuery query, CancellationToken cancellationToken)
    {
        var entries = context.Set<CommandHistoryEntity>()
            .AsNoTracking()
            .Where(entry => entry.ConnectionId == query.ConnectionId);
        if (query.Search is not null)
        {
            var pattern = $"%{EscapeLike(query.Search)}%";
            entries = entries.Where(entry => EF.Functions.Like(entry.Command, pattern, LikeEscape));
        }

        var entities = await entries
            .OrderByDescending(entry => entry.IsPinned)
            .ThenByDescending(entry => entry.ExecutedAt)
            .Take(query.Limit)
            .ToListAsync(cancellationToken);
        var models = new List<CommandHistoryEntry>(entities.Count);
        for (var i = 0; i < entities.Count; i++)
        {
            models.Add(CommandHistoryMapper.ToModel(entities[i]));
        }

        return models;
    }

    public async Task<bool> SetPinnedAsync(Guid id, bool isPinned, CancellationToken cancellationToken)
    {
        var updated = await context.Set<CommandHistoryEntity>()
            .Where(entry => entry.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(entry => entry.IsPinned, isPinned), cancellationToken);
        return updated > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await context.Set<CommandHistoryEntity>()
            .Where(entry => entry.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
        return deleted > 0;
    }

    public Task<int> ClearUnpinnedAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        return context.Set<CommandHistoryEntity>()
            .Where(entry => entry.ConnectionId == connectionId && !entry.IsPinned)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<int> PruneUnpinnedAsync(Guid connectionId, int keepNewest, CancellationToken cancellationToken)
    {
        var surplusIds = await context.Set<CommandHistoryEntity>()
            .Where(entry => entry.ConnectionId == connectionId && !entry.IsPinned)
            .OrderByDescending(entry => entry.ExecutedAt)
            .Skip(keepNewest)
            .Select(entry => entry.Id)
            .ToListAsync(cancellationToken);
        if (surplusIds.Count == 0)
        {
            return 0;
        }

        return await context.Set<CommandHistoryEntity>()
            .Where(entry => surplusIds.Contains(entry.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static string EscapeLike(string text)
    {
        return text
            .Replace(LikeEscape, LikeEscape + LikeEscape, StringComparison.Ordinal)
            .Replace("%", LikeEscape + "%", StringComparison.Ordinal)
            .Replace("_", LikeEscape + "_", StringComparison.Ordinal);
    }
}
