using DTech.Core.SavedConnections;

namespace DTech.Core.Workspace;

public sealed class WorkspaceConnectionFilter(ISavedConnectionRepository repository)
{
    public async Task<WorkspaceSnapshot> ApplyAsync(WorkspaceSnapshot snapshot, CancellationToken cancellationToken)
    {
        var connections = await repository.ListAsync(cancellationToken);
        var known = new HashSet<Guid>();
        for (var i = 0; i < connections.Count; i++)
        {
            known.Add(connections[i].Id);
        }

        var state = snapshot.State;
        var terminalTabs = state.TerminalTabs.Where(tab => known.Contains(tab.ConnectionId)).ToList();
        var activeTerminalKey = terminalTabs.Any(tab => tab.Key == state.ActiveTerminalKey) ? state.ActiveTerminalKey : null;
        var editorTabs = state.EditorTabs.Where(tab => known.Contains(tab.ConnectionId)).ToList();
        var activeEditorTab = state.ActiveEditorTab is not null && known.Contains(state.ActiveEditorTab.ConnectionId) ? state.ActiveEditorTab : null;
        var selectedConnectionId = state.SelectedConnectionId is { } selected && known.Contains(selected) ? selected : (Guid?)null;
        var filteredState = state with
        {
            SelectedConnectionId = selectedConnectionId,
            TerminalTabs = terminalTabs,
            ActiveTerminalKey = activeTerminalKey,
            RemotePaths = state.RemotePaths.Where(path => known.Contains(path.ConnectionId)).ToList(),
            EditorTabs = editorTabs,
            ActiveEditorTab = activeEditorTab,
        };
        var drafts = snapshot.Drafts.Where(draft => known.Contains(draft.ConnectionId)).ToList();
        return new WorkspaceSnapshot(filteredState, drafts);
    }
}
