using System.Text.Json;

namespace DTech.Core.Workspace;

public static class WorkspaceStateSchema
{
    public const int CurrentVersion = 1;

    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web);
}
