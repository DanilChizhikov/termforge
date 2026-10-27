using System.Text.Json.Nodes;

namespace DTech.Core.Workspace;

public sealed class WorkspaceStateUpgrader
{
    private readonly Dictionary<int, IWorkspaceStateUpgradeStep> _stepsByVersion = new();

    public WorkspaceStateUpgrader(IEnumerable<IWorkspaceStateUpgradeStep> steps)
    {
        foreach (var step in steps)
        {
            _stepsByVersion[step.FromVersion] = step;
        }
    }

    public JsonObject? Upgrade(JsonObject document, int fromVersion, int toVersion)
    {
        var upgraded = document;
        for (var version = fromVersion; version < toVersion; version++)
        {
            if (!_stepsByVersion.TryGetValue(version, out var step))
            {
                return null;
            }

            upgraded = step.Upgrade(upgraded);
        }

        return upgraded;
    }
}
