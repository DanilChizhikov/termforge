using System.Text.Json;
using System.Text.Json.Nodes;

namespace DTech.Core.Workspace;

public sealed class WorkspaceStateReader(WorkspaceStateUpgrader upgrader)
{
    public WorkspaceStateReadResult Read(StoredWorkspaceDocument document)
    {
        if (document.SchemaVersion > WorkspaceStateSchema.CurrentVersion)
        {
            return UnsupportedVersion(document.SchemaVersion);
        }

        var parsed = Parse(document.Json);
        if (parsed is null)
        {
            return Corrupted();
        }

        var upgraded = upgrader.Upgrade(parsed, document.SchemaVersion, WorkspaceStateSchema.CurrentVersion);
        if (upgraded is null)
        {
            return UnsupportedVersion(document.SchemaVersion);
        }

        var state = Deserialize(upgraded);
        if (WorkspaceStateValidator.Validate(state) is not null)
        {
            return Corrupted();
        }

        return WorkspaceStateReadResult.Success(state!);
    }

    private static JsonObject? Parse(string json)
    {
        try
        {
            return JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static WorkspaceState? Deserialize(JsonObject document)
    {
        try
        {
            return document.Deserialize<WorkspaceState>(WorkspaceStateSchema.JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private static WorkspaceStateReadResult Corrupted()
    {
        var warning = WorkspaceRestoreWarning.Corrupted();
        return WorkspaceStateReadResult.Failure(warning);
    }

    private static WorkspaceStateReadResult UnsupportedVersion(int version)
    {
        var warning = WorkspaceRestoreWarning.UnsupportedVersion(version);
        return WorkspaceStateReadResult.Failure(warning);
    }
}
