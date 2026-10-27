using DTech.Core.SavedConnections;
using DTech.Persistence.Database;
using Microsoft.EntityFrameworkCore;

namespace DTech.Persistence.SavedConnections;

public sealed class SavedConnectionRepository(TermForgeDbContext context) : ISavedConnectionRepository
{
    public async Task<IReadOnlyList<SavedConnection>> ListAsync(CancellationToken cancellationToken)
    {
        var entities = await context.Set<SavedConnectionEntity>()
            .AsNoTracking()
            .OrderBy(connection => connection.Name)
            .ToListAsync(cancellationToken);
        var models = new List<SavedConnection>(entities.Count);
        for (var i = 0; i < entities.Count; i++)
        {
            models.Add(SavedConnectionMapper.ToModel(entities[i]));
        }

        return models;
    }

    public async Task<SavedConnection?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await context.Set<SavedConnectionEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(connection => connection.Id == id, cancellationToken);
        return entity is null ? null : SavedConnectionMapper.ToModel(entity);
    }

    public async Task AddAsync(SavedConnection connection, CancellationToken cancellationToken)
    {
        var entity = SavedConnectionMapper.ToEntity(connection);
        context.Set<SavedConnectionEntity>().Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        context.Entry(entity).State = EntityState.Detached;
    }

    public async Task<bool> UpdateAsync(SavedConnection connection, CancellationToken cancellationToken)
    {
        var entity = await context.Set<SavedConnectionEntity>().FirstOrDefaultAsync(item => item.Id == connection.Id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        SavedConnectionMapper.CopyTo(connection, entity);
        await context.SaveChangesAsync(cancellationToken);
        context.Entry(entity).State = EntityState.Detached;
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await context.Set<SavedConnectionEntity>()
            .Where(connection => connection.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
        return deleted > 0;
    }

    public async Task<bool> RecordConnectedAsync(Guid id, DateTime connectedAt, CancellationToken cancellationToken)
    {
        var updated = await context.Set<SavedConnectionEntity>()
            .Where(connection => connection.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(connection => connection.LastConnectedAt, connectedAt), cancellationToken);
        return updated > 0;
    }

    public Task<int> RenameGroupAsync(string currentName, string newName, CancellationToken cancellationToken)
    {
        return context.Set<SavedConnectionEntity>()
            .Where(connection => connection.GroupName == currentName)
            .ExecuteUpdateAsync(setters => setters.SetProperty(connection => connection.GroupName, newName), cancellationToken);
    }

    public Task<int> RemoveGroupAsync(string name, CancellationToken cancellationToken)
    {
        return context.Set<SavedConnectionEntity>()
            .Where(connection => connection.GroupName == name)
            .ExecuteUpdateAsync(setters => setters.SetProperty(connection => connection.GroupName, (string?)null), cancellationToken);
    }
}
