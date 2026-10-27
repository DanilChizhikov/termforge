using System.Text.Json.Nodes;
using DTech.Core.Workspace;
using FluentAssertions;

namespace DTech.Core.Tests.Workspace;

[TestClass]
public sealed class WorkspaceStateUpgraderTests
{
    [TestMethod]
    public void Upgrade_AppliesEveryStepInVersionOrder()
    {
        var steps = new IWorkspaceStateUpgradeStep[] { new AppendStep(2), new AppendStep(1) };
        var upgrader = new WorkspaceStateUpgrader(steps);
        var document = new JsonObject { ["trail"] = "" };

        var upgraded = upgrader.Upgrade(document, 1, 3);

        upgraded!["trail"]!.GetValue<string>().Should().Be("12");
    }

    [TestMethod]
    public void Upgrade_WithAMissingStep_ReturnsNothing()
    {
        var steps = new IWorkspaceStateUpgradeStep[] { new AppendStep(1) };
        var upgrader = new WorkspaceStateUpgrader(steps);
        var document = new JsonObject { ["trail"] = "" };

        var upgraded = upgrader.Upgrade(document, 1, 3);

        upgraded.Should().BeNull();
    }

    [TestMethod]
    public void Upgrade_CurrentVersion_ReturnsTheDocumentUnchanged()
    {
        var upgrader = new WorkspaceStateUpgrader([]);
        var document = new JsonObject { ["trail"] = "x" };

        var upgraded = upgrader.Upgrade(document, 2, 2);

        upgraded.Should().BeSameAs(document);
    }

    private sealed class AppendStep(int fromVersion) : IWorkspaceStateUpgradeStep
    {
        public int FromVersion => fromVersion;

        public JsonObject Upgrade(JsonObject document)
        {
            var trail = document["trail"]!.GetValue<string>();
            document["trail"] = $"{trail}{fromVersion}";
            return document;
        }
    }
}
