namespace DTech.Core.Workspace;

public sealed record WorkspaceSnapshot(WorkspaceState State, IReadOnlyList<WorkspaceEditorDraft> Drafts)
{
    public static WorkspaceSnapshot Empty { get; } = new(WorkspaceState.Empty, []);
}
