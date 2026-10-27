using System.Text.Json.Nodes;

namespace DTech.Core.Workspace;

public interface IWorkspaceStateUpgradeStep
{
    int FromVersion { get; }

    JsonObject Upgrade(JsonObject document);
}
