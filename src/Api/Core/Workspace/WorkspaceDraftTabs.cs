namespace DTech.Core.Workspace;

public static class WorkspaceDraftTabs
{
    public static WorkspaceState IncludeDraftTabs(WorkspaceState state, IReadOnlyList<WorkspaceEditorDraft> drafts)
    {
        var editorTabs = new List<WorkspaceEditorTab>(state.EditorTabs);
        var openTabs = new HashSet<WorkspaceEditorTab>(state.EditorTabs);
        for (var i = 0; i < drafts.Count; i++)
        {
            var tab = drafts[i].ToTab();
            if (openTabs.Add(tab))
            {
                editorTabs.Add(tab);
            }
        }

        return state with { EditorTabs = editorTabs };
    }
}
