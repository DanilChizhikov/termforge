using System.Text.Json.Nodes;
using DTech.Core.Workspace;
using FluentAssertions;

namespace DTech.Core.Tests.Workspace;

[TestClass]
public sealed class WorkspaceStateReaderTests
{
    [TestMethod]
    public void Read_SerializedCurrentState_RoundTripsEveryField()
    {
        var state = WorkspaceSamples.State();
        var document = WorkspaceStateSerializer.Serialize(state);
        var reader = CreateReader();

        var read = reader.Read(document);

        document.SchemaVersion.Should().Be(WorkspaceStateSchema.CurrentVersion);
        read.IsSuccess.Should().BeTrue();
        read.State.Should().BeEquivalentTo(state);
    }

    [TestMethod]
    [DataRow("{not json")]
    [DataRow("[1,2,3]")]
    [DataRow("null")]
    [DataRow("{\"terminalTabs\":[{\"key\":\"\",\"connectionId\":\"0f8fad5b-d9cb-469f-a165-70867728950e\",\"connectionName\":\"web\"}]}")]
    [DataRow("{\"terminalTabs\":null}")]
    [DataRow("{\"panelLayouts\":[{\"groupId\":\"app-layout\",\"sizes\":{\"main\":250}}]}")]
    [DataRow("{\"selectedConnectionId\":\"not-a-guid\"}")]
    public void Read_CorruptedDocument_FailsWithCorruptedWarning(string json)
    {
        var document = new StoredWorkspaceDocument(WorkspaceStateSchema.CurrentVersion, json);
        var reader = CreateReader();

        var read = reader.Read(document);

        read.IsSuccess.Should().BeFalse();
        read.Warning!.Code.Should().Be(WorkspaceRestoreWarningCode.Corrupted);
    }

    [TestMethod]
    public void Read_DocumentFromNewerVersion_FailsWithUnsupportedVersionWarning()
    {
        var newerVersion = WorkspaceStateSchema.CurrentVersion + 1;
        var document = new StoredWorkspaceDocument(newerVersion, "{}");
        var reader = CreateReader();

        var read = reader.Read(document);

        read.IsSuccess.Should().BeFalse();
        read.Warning!.Code.Should().Be(WorkspaceRestoreWarningCode.UnsupportedVersion);
        read.Warning.Message.Should().Contain(newerVersion.ToString());
    }

    [TestMethod]
    public void Read_OlderDocumentWithoutUpgradeStep_FailsWithUnsupportedVersionWarning()
    {
        var olderVersion = WorkspaceStateSchema.CurrentVersion - 1;
        var document = new StoredWorkspaceDocument(olderVersion, "{}");
        var reader = CreateReader();

        var read = reader.Read(document);

        read.Warning!.Code.Should().Be(WorkspaceRestoreWarningCode.UnsupportedVersion);
    }

    [TestMethod]
    public void Read_OlderDocument_IsUpgradedByTheRegisteredStep()
    {
        var olderVersion = WorkspaceStateSchema.CurrentVersion - 1;
        var legacyJson = $"{{\"selectedServer\":\"{WorkspaceSamples.WebId}\",\"localPath\":\"/home/dev\"}}";
        var document = new StoredWorkspaceDocument(olderVersion, legacyJson);
        var step = new RenameSelectedServerStep(olderVersion);
        var reader = CreateReader(step);

        var read = reader.Read(document);

        read.IsSuccess.Should().BeTrue();
        read.State!.SelectedConnectionId.Should().Be(WorkspaceSamples.WebId);
        read.State.LocalPath.Should().Be("/home/dev");
    }

    private static WorkspaceStateReader CreateReader(params IWorkspaceStateUpgradeStep[] steps)
    {
        var upgrader = new WorkspaceStateUpgrader(steps);
        return new WorkspaceStateReader(upgrader);
    }

    private sealed class RenameSelectedServerStep(int fromVersion) : IWorkspaceStateUpgradeStep
    {
        public int FromVersion => fromVersion;

        public JsonObject Upgrade(JsonObject document)
        {
            var selectedServer = document["selectedServer"]?.DeepClone();
            document.Remove("selectedServer");
            document["selectedConnectionId"] = selectedServer;
            return document;
        }
    }
}
