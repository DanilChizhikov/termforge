using DTech.Core.SavedConnections;

namespace DTech.Core.Tests.SavedConnections.Support;

internal sealed class InMemorySavedConnectionRepository : ISavedConnectionRepository
{
    private readonly List<SavedConnection> _connections = [];

    public IReadOnlyList<SavedConnection> Snapshot => _connections.ToList();

    public Task<IReadOnlyList<SavedConnection>> ListAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<SavedConnection> copy = _connections.OrderBy(connection => connection.Details.Name).ToList();
        return Task.FromResult(copy);
    }

    public Task<SavedConnection?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        var found = _connections.FirstOrDefault(connection => connection.Id == id);
        return Task.FromResult(found);
    }

    public Task AddAsync(SavedConnection connection, CancellationToken cancellationToken)
    {
        _connections.Add(connection);
        return Task.CompletedTask;
    }

    public Task<bool> UpdateAsync(SavedConnection connection, CancellationToken cancellationToken)
    {
        var index = _connections.FindIndex(item => item.Id == connection.Id);
        if (index < 0)
        {
            return Task.FromResult(false);
        }

        _connections[index] = connection;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var removed = _connections.RemoveAll(connection => connection.Id == id);
        return Task.FromResult(removed > 0);
    }

    public Task<bool> RecordConnectedAsync(Guid id, DateTime connectedAt, CancellationToken cancellationToken)
    {
        var index = _connections.FindIndex(item => item.Id == id);
        if (index < 0)
        {
            return Task.FromResult(false);
        }

        _connections[index] = _connections[index] with { LastConnectedAt = connectedAt };
        return Task.FromResult(true);
    }

    public Task<int> RenameGroupAsync(string currentName, string newName, CancellationToken cancellationToken)
    {
        var changed = 0;
        for (var i = 0; i < _connections.Count; i++)
        {
            if (_connections[i].Details.GroupName == currentName)
            {
                var details = _connections[i].Details with { GroupName = newName };
                _connections[i] = _connections[i] with { Details = details };
                changed++;
            }
        }

        return Task.FromResult(changed);
    }

    public Task<int> RemoveGroupAsync(string name, CancellationToken cancellationToken)
    {
        var changed = 0;
        for (var i = 0; i < _connections.Count; i++)
        {
            if (_connections[i].Details.GroupName == name)
            {
                var details = _connections[i].Details with { GroupName = null };
                _connections[i] = _connections[i] with { Details = details };
                changed++;
            }
        }

        return Task.FromResult(changed);
    }
}
