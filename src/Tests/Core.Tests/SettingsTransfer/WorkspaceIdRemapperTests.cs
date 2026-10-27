using DTech.Core.SavedConnections;
using DTech.Core.SettingsTransfer.Import;
using DTech.Core.Workspace;
using FluentAssertions;

namespace DTech.Core.Tests.SettingsTransfer;

[TestClass]
public sealed class WorkspaceIdRemapperTests
{
    [TestMethod]
    public void Remap_PointsReferencesToTheLocalServersAndDropsUnknownOnes()
    {
        var existing = SettingsSamples.Saved("web");
        var importedWeb = SettingsSamples.Connection("web");
        var missingId = Guid.NewGuid();
        var editorTab = new WorkspaceEditorTab(importedWeb.Id, "/etc/hosts");
        var knownTerminal = new WorkspaceTerminalTab("terminal-1", importedWeb.Id, "old name");
        var orphanTerminal = new WorkspaceTerminalTab("terminal-2", missingId, "gone");
        var knownPath = new WorkspaceRemotePath(importedWeb.Id, "/var/www");
        var orphanPath = new WorkspaceRemotePath(missingId, "/srv");
        var state = WorkspaceState.Empty with
        {
            SelectedConnectionId = importedWeb.Id,
            TerminalTabs = [knownTerminal, orphanTerminal],
            ActiveTerminalKey = "terminal-2",
            RemotePaths = [knownPath, orphanPath],
            EditorTabs = [editorTab],
            ActiveEditorTab = editorTab,
        };
        var document = SettingsSamples.Document(importedWeb);
        var options = SettingsSamples.Options(ImportMode.Merge);
        SavedConnection[] saved = [existing];
        var plan = SettingsImportPlanner.Plan(document, saved, options).Value;

        var remapped = WorkspaceIdRemapper.Remap(state, plan);

        remapped.SelectedConnectionId.Should().Be(existing.Id);
        var terminal = remapped.TerminalTabs.Should().ContainSingle().Subject;
        terminal.ConnectionId.Should().Be(existing.Id);
        terminal.ConnectionName.Should().Be("web");
        remapped.ActiveTerminalKey.Should().BeNull();
        remapped.RemotePaths.Should().ContainSingle().Which.ConnectionId.Should().Be(existing.Id);
        remapped.EditorTabs.Should().ContainSingle().Which.ConnectionId.Should().Be(existing.Id);
        remapped.ActiveEditorTab!.ConnectionId.Should().Be(existing.Id);
    }
}
