using DTech.Core.CommandHistory;
using DTech.Core.SavedConnections;

namespace DTech.Core.SettingsTransfer.Export;

public sealed class PinnedCommandReader(ICommandHistoryRepository repository)
{
    public async Task<IReadOnlyList<CommandHistoryEntry>> ReadAsync(IReadOnlyList<SavedConnection> connections, CancellationToken cancellationToken)
    {
        var pinned = new List<CommandHistoryEntry>();
        for (var i = 0; i < connections.Count; i++)
        {
            var query = new CommandHistoryQuery(connections[i].Id, null, SettingsTransferLimits.MaxPinnedCommandsPerConnection);
            var entries = await repository.ListAsync(query, cancellationToken);
            for (var j = 0; j < entries.Count; j++)
            {
                var entry = entries[j];
                if (entry.IsPinned)
                {
                    pinned.Add(entry);
                }
            }
        }

        return pinned;
    }
}
