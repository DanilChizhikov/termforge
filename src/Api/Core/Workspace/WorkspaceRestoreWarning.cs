namespace DTech.Core.Workspace;

public sealed record WorkspaceRestoreWarning(WorkspaceRestoreWarningCode Code, string Message)
{
    public static WorkspaceRestoreWarning Corrupted()
    {
        return new WorkspaceRestoreWarning(WorkspaceRestoreWarningCode.Corrupted, "The saved workspace could not be read and was reset. Unsaved editor changes were kept.");
    }

    public static WorkspaceRestoreWarning UnsupportedVersion(int version)
    {
        return new WorkspaceRestoreWarning(WorkspaceRestoreWarningCode.UnsupportedVersion, $"The saved workspace uses format version {version}, which this version of TermForge cannot read. It was reset. Unsaved editor changes were kept.");
    }
}
