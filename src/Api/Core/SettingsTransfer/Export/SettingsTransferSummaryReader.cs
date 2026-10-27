using DTech.Core.SavedConnections;

namespace DTech.Core.SettingsTransfer.Export;

public sealed class SettingsTransferSummaryReader(ISavedConnectionRepository repository, PinnedCommandReader pinnedCommandReader)
{
    public async Task<SettingsTransferSummary> ReadAsync(CancellationToken cancellationToken)
    {
        var connections = await repository.ListAsync(cancellationToken);
        var groups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < connections.Count; i++)
        {
            var groupName = connections[i].Details.GroupName;
            if (groupName is not null)
            {
                groups.Add(groupName);
            }
        }

        var pinned = await pinnedCommandReader.ReadAsync(connections, cancellationToken);
        return new SettingsTransferSummary(connections.Count, groups.Count, pinned.Count);
    }
}
