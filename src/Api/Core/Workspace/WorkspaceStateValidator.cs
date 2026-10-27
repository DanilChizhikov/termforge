namespace DTech.Core.Workspace;

public static class WorkspaceStateValidator
{
    public static WorkspaceStateError? Validate(WorkspaceState? state)
    {
        if (state is null)
        {
            return new WorkspaceStateError("The workspace state is missing.");
        }

        if (state.SelectedConnectionId == Guid.Empty)
        {
            return new WorkspaceStateError("The selected server id is invalid.");
        }

        if (state.ActiveView is not null && !WorkspaceValueRules.IsIdentifier(state.ActiveView))
        {
            return new WorkspaceStateError("The active view is invalid.");
        }

        if (state.LocalPath is not null && !WorkspaceValueRules.IsPath(state.LocalPath))
        {
            return new WorkspaceStateError("The local path is invalid.");
        }

        if (state.ActiveTerminalKey is not null && !WorkspaceValueRules.IsIdentifier(state.ActiveTerminalKey))
        {
            return new WorkspaceStateError("The active terminal key is invalid.");
        }

        if (state.ActiveEditorTab is not null && !WorkspaceValueRules.IsEditorTab(state.ActiveEditorTab))
        {
            return new WorkspaceStateError("The active editor tab is invalid.");
        }

        return ValidateTerminalTabs(state.TerminalTabs)
            ?? ValidateRemotePaths(state.RemotePaths)
            ?? ValidatePanelLayouts(state.PanelLayouts)
            ?? ValidateCollapsedPanels(state.CollapsedPanels)
            ?? ValidateEditorTabs(state.EditorTabs);
    }

    private static WorkspaceStateError? ValidateTerminalTabs(IReadOnlyList<WorkspaceTerminalTab>? tabs)
    {
        if (tabs is null || tabs.Count > WorkspaceStateLimits.MaxTerminalTabs)
        {
            return new WorkspaceStateError($"Up to {WorkspaceStateLimits.MaxTerminalTabs} terminal tabs can be saved.");
        }

        var keys = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < tabs.Count; i++)
        {
            var tab = tabs[i];
            var valid = tab is not null
                && WorkspaceValueRules.IsIdentifier(tab.Key)
                && tab.ConnectionId != Guid.Empty
                && tab.ConnectionName is not null
                && tab.ConnectionName.Length <= WorkspaceStateLimits.MaxConnectionNameLength
                && keys.Add(tab.Key);
            if (!valid)
            {
                return new WorkspaceStateError("A terminal tab is invalid or duplicated.");
            }
        }

        return null;
    }

    private static WorkspaceStateError? ValidateRemotePaths(IReadOnlyList<WorkspaceRemotePath>? paths)
    {
        if (paths is null || paths.Count > WorkspaceStateLimits.MaxRemotePaths)
        {
            return new WorkspaceStateError($"Up to {WorkspaceStateLimits.MaxRemotePaths} remote paths can be saved.");
        }

        for (var i = 0; i < paths.Count; i++)
        {
            var remotePath = paths[i];
            var valid = remotePath is not null && remotePath.ConnectionId != Guid.Empty && WorkspaceValueRules.IsPath(remotePath.Path);
            if (!valid)
            {
                return new WorkspaceStateError("A remote path is invalid.");
            }
        }

        return null;
    }

    private static WorkspaceStateError? ValidatePanelLayouts(IReadOnlyList<WorkspacePanelLayout>? layouts)
    {
        if (layouts is null || layouts.Count > WorkspaceStateLimits.MaxPanelLayouts)
        {
            return new WorkspaceStateError($"Up to {WorkspaceStateLimits.MaxPanelLayouts} panel layouts can be saved.");
        }

        for (var i = 0; i < layouts.Count; i++)
        {
            var layout = layouts[i];
            var valid = layout is not null && WorkspaceValueRules.IsIdentifier(layout.GroupId) && HasValidSizes(layout.Sizes);
            if (!valid)
            {
                return new WorkspaceStateError("A panel layout is invalid.");
            }
        }

        return null;
    }

    private static bool HasValidSizes(IReadOnlyDictionary<string, double>? sizes)
    {
        if (sizes is null || sizes.Count > WorkspaceStateLimits.MaxPanelsPerLayout)
        {
            return false;
        }

        foreach (var size in sizes)
        {
            var valid = WorkspaceValueRules.IsIdentifier(size.Key) && double.IsFinite(size.Value) && size.Value is >= 0 and <= 100;
            if (!valid)
            {
                return false;
            }
        }

        return true;
    }

    private static WorkspaceStateError? ValidateCollapsedPanels(IReadOnlyList<string>? panels)
    {
        if (panels is null || panels.Count > WorkspaceStateLimits.MaxCollapsedPanels)
        {
            return new WorkspaceStateError($"Up to {WorkspaceStateLimits.MaxCollapsedPanels} collapsed panels can be saved.");
        }

        for (var i = 0; i < panels.Count; i++)
        {
            if (!WorkspaceValueRules.IsIdentifier(panels[i]))
            {
                return new WorkspaceStateError("A collapsed panel id is invalid.");
            }
        }

        return null;
    }

    private static WorkspaceStateError? ValidateEditorTabs(IReadOnlyList<WorkspaceEditorTab>? tabs)
    {
        if (tabs is null || tabs.Count > WorkspaceStateLimits.MaxEditorTabs)
        {
            return new WorkspaceStateError($"Up to {WorkspaceStateLimits.MaxEditorTabs} editor tabs can be saved.");
        }

        var seen = new HashSet<WorkspaceEditorTab>();
        for (var i = 0; i < tabs.Count; i++)
        {
            var tab = tabs[i];
            if (!WorkspaceValueRules.IsEditorTab(tab) || !seen.Add(tab))
            {
                return new WorkspaceStateError("An editor tab is invalid or duplicated.");
            }
        }

        return null;
    }
}
