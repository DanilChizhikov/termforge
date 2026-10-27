using DTech.Core.Workspace;

namespace DTech.Core.SettingsTransfer.Import;

public static class WorkspaceIdRemapper
{
    public static WorkspaceState Remap(WorkspaceState state, SettingsImportPlan plan)
    {
        var terminalTabs = new List<WorkspaceTerminalTab>();
        for (var i = 0; i < state.TerminalTabs.Count; i++)
        {
            var tab = state.TerminalTabs[i];
            if (plan.MapConnectionId(tab.ConnectionId) is { } localId)
            {
                var remapped = tab with { ConnectionId = localId, ConnectionName = plan.FinalConnectionNames[localId] };
                terminalTabs.Add(remapped);
            }
        }

        var remotePaths = new List<WorkspaceRemotePath>();
        for (var i = 0; i < state.RemotePaths.Count; i++)
        {
            var remotePath = state.RemotePaths[i];
            if (plan.MapConnectionId(remotePath.ConnectionId) is { } localId)
            {
                var remapped = remotePath with { ConnectionId = localId };
                remotePaths.Add(remapped);
            }
        }

        var editorTabs = new List<WorkspaceEditorTab>();
        for (var i = 0; i < state.EditorTabs.Count; i++)
        {
            var remapped = RemapEditorTab(state.EditorTabs[i], plan);
            if (remapped is not null)
            {
                editorTabs.Add(remapped);
            }
        }

        var activeTerminalKey = terminalTabs.Any(tab => tab.Key == state.ActiveTerminalKey) ? state.ActiveTerminalKey : null;
        var selectedConnectionId = state.SelectedConnectionId is { } selected ? plan.MapConnectionId(selected) : null;
        var activeEditorTab = state.ActiveEditorTab is null ? null : RemapEditorTab(state.ActiveEditorTab, plan);
        return state with
        {
            SelectedConnectionId = selectedConnectionId,
            TerminalTabs = terminalTabs,
            ActiveTerminalKey = activeTerminalKey,
            RemotePaths = remotePaths,
            EditorTabs = editorTabs,
            ActiveEditorTab = activeEditorTab,
        };
    }

    private static WorkspaceEditorTab? RemapEditorTab(WorkspaceEditorTab tab, SettingsImportPlan plan)
    {
        return plan.MapConnectionId(tab.ConnectionId) is { } localId ? tab with { ConnectionId = localId } : null;
    }
}
