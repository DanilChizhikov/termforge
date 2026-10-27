using DTech.Core.Editor;

namespace DTech.Core.Workspace;

public sealed record WorkspaceEditorDraft(Guid ConnectionId, string Path, string Content, RemoteFileVersion? BaseVersion)
{
    public WorkspaceEditorTab ToTab()
    {
        return new WorkspaceEditorTab(ConnectionId, Path);
    }
}
