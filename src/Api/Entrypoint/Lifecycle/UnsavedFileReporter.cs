using DTech.Core.SavedConnections;
using DTech.Core.Workspace;

namespace DTech.Entrypoint.Lifecycle;

internal sealed class UnsavedFileReporter(IWorkspaceStateStore workspaceStore, ISavedConnectionRepository connections)
{
    public async Task<IReadOnlyList<UnsavedFileResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var drafts = await workspaceStore.LoadDraftsAsync(cancellationToken);
        if (drafts.Count == 0)
        {
            return [];
        }

        var savedConnections = await connections.ListAsync(cancellationToken);
        var serverNames = new Dictionary<Guid, string>();
        for (var i = 0; i < savedConnections.Count; i++)
        {
            var connection = savedConnections[i];
            serverNames[connection.Id] = connection.Details.Name;
        }

        var unsavedFiles = new List<UnsavedFileResponse>(drafts.Count);
        for (var i = 0; i < drafts.Count; i++)
        {
            var draft = drafts[i];
            if (!serverNames.TryGetValue(draft.ConnectionId, out var serverName))
            {
                continue;
            }

            var unsavedFile = new UnsavedFileResponse(draft.ConnectionId, serverName, draft.Path);
            unsavedFiles.Add(unsavedFile);
        }

        return unsavedFiles;
    }
}
