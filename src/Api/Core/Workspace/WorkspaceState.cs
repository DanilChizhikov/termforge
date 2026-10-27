namespace DTech.Core.Workspace;

public sealed record WorkspaceState
{
    public static WorkspaceState Empty { get; } = new();

    public Guid? SelectedConnectionId { get; init; }

    public string? ActiveView { get; init; }

    public IReadOnlyList<WorkspaceTerminalTab> TerminalTabs { get; init; } = [];

    public string? ActiveTerminalKey { get; init; }

    public string? LocalPath { get; init; }

    public IReadOnlyList<WorkspaceRemotePath> RemotePaths { get; init; } = [];

    public IReadOnlyList<WorkspacePanelLayout> PanelLayouts { get; init; } = [];

    public IReadOnlyList<string> CollapsedPanels { get; init; } = [];

    public IReadOnlyList<WorkspaceEditorTab> EditorTabs { get; init; } = [];

    public WorkspaceEditorTab? ActiveEditorTab { get; init; }
}
