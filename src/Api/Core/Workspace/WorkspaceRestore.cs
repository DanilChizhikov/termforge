namespace DTech.Core.Workspace;

public sealed record WorkspaceRestore(WorkspaceSnapshot Snapshot, WorkspaceRestoreWarning? Warning);
