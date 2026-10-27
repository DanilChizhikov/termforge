using DTech.Core.Workspace;

namespace DTech.Entrypoint.Workspace;

internal sealed record WorkspaceRestoreResponse(WorkspaceState State, IReadOnlyList<WorkspaceEditorDraft> Drafts, WorkspaceWarningResponse? Warning)
{
    public static WorkspaceRestoreResponse From(WorkspaceRestore restore)
    {
        var warning = restore.Warning is null ? null : new WorkspaceWarningResponse(restore.Warning.Code.ToString(), restore.Warning.Message);
        return new WorkspaceRestoreResponse(restore.Snapshot.State, restore.Snapshot.Drafts, warning);
    }
}
