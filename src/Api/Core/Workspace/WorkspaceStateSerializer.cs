using System.Text.Json;

namespace DTech.Core.Workspace;

public static class WorkspaceStateSerializer
{
    public static StoredWorkspaceDocument Serialize(WorkspaceState state)
    {
        var json = JsonSerializer.Serialize(state, WorkspaceStateSchema.JsonOptions);
        return new StoredWorkspaceDocument(WorkspaceStateSchema.CurrentVersion, json);
    }
}
