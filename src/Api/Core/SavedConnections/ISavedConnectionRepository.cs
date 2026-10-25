namespace DTech.Core.SavedConnections;

public interface ISavedConnectionRepository
{
    Task<IReadOnlyList<SavedConnection>> ListAsync(CancellationToken cancellationToken);

    Task<SavedConnection?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task AddAsync(SavedConnection connection, CancellationToken cancellationToken);

    Task<bool> UpdateAsync(SavedConnection connection, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> RecordConnectedAsync(Guid id, DateTime connectedAt, CancellationToken cancellationToken);

    Task<int> RenameGroupAsync(string currentName, string newName, CancellationToken cancellationToken);

    Task<int> RemoveGroupAsync(string name, CancellationToken cancellationToken);
}
