using DTech.Core.SavedConnections;
using DTech.Core.Workspace;
using FluentAssertions;
using NSubstitute;

namespace DTech.Core.Tests.Workspace;

[TestClass]
public sealed class WorkspaceStateLoaderTests
{
    private readonly IWorkspaceStateStore _store = Substitute.For<IWorkspaceStateStore>();
    private readonly ISavedConnectionRepository _connections = Substitute.For<ISavedConnectionRepository>();

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Initialize()
    {
        var web = WorkspaceSamples.Connection(WorkspaceSamples.WebId, "web");
        var database = WorkspaceSamples.Connection(WorkspaceSamples.DatabaseId, "db");
        IReadOnlyList<SavedConnection> connections = [web, database];
        _connections.ListAsync(Arg.Any<CancellationToken>()).Returns(connections);
        IReadOnlyList<WorkspaceEditorDraft> noDrafts = [];
        _store.LoadDraftsAsync(Arg.Any<CancellationToken>()).Returns(noDrafts);
    }

    [TestMethod]
    public async Task Load_WithoutSavedState_ReturnsAnEmptyWorkspace()
    {
        var loader = CreateLoader();

        var restore = await loader.LoadAsync(TestContext.CancellationToken);

        restore.Warning.Should().BeNull();
        restore.Snapshot.State.Should().BeEquivalentTo(WorkspaceState.Empty);
        restore.Snapshot.Drafts.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Load_SavedState_RestoresStateAndUnsavedDrafts()
    {
        var state = WorkspaceSamples.State();
        var draft = WorkspaceSamples.Draft(WorkspaceSamples.WebId, "/etc/nginx/nginx.conf");
        var document = WorkspaceStateSerializer.Serialize(state);
        _store.LoadDocumentAsync(Arg.Any<CancellationToken>()).Returns(document);
        IReadOnlyList<WorkspaceEditorDraft> drafts = [draft];
        _store.LoadDraftsAsync(Arg.Any<CancellationToken>()).Returns(drafts);
        var loader = CreateLoader();

        var restore = await loader.LoadAsync(TestContext.CancellationToken);

        restore.Warning.Should().BeNull();
        restore.Snapshot.State.Should().BeEquivalentTo(state);
        restore.Snapshot.Drafts.Should().Equal(draft);
    }

    [TestMethod]
    public async Task Load_CorruptedState_ResetsItWithAWarningAndKeepsUnsavedDrafts()
    {
        var corrupted = new StoredWorkspaceDocument(WorkspaceStateSchema.CurrentVersion, "{\"terminalTabs\":");
        _store.LoadDocumentAsync(Arg.Any<CancellationToken>()).Returns(corrupted);
        var draft = WorkspaceSamples.Draft(WorkspaceSamples.DatabaseId, "/etc/hosts");
        IReadOnlyList<WorkspaceEditorDraft> drafts = [draft];
        _store.LoadDraftsAsync(Arg.Any<CancellationToken>()).Returns(drafts);
        var loader = CreateLoader();

        var restore = await loader.LoadAsync(TestContext.CancellationToken);

        restore.Warning!.Code.Should().Be(WorkspaceRestoreWarningCode.Corrupted);
        await _store.Received(1).DeleteDocumentAsync(Arg.Any<CancellationToken>());
        restore.Snapshot.State.TerminalTabs.Should().BeEmpty();
        restore.Snapshot.State.EditorTabs.Should().Equal(draft.ToTab());
        restore.Snapshot.Drafts.Should().Equal(draft);
    }

    [TestMethod]
    public async Task Load_StateReferencingADeletedServer_DropsEverythingBoundToIt()
    {
        var deletedId = Guid.NewGuid();
        var state = WorkspaceSamples.State() with
        {
            SelectedConnectionId = deletedId,
            TerminalTabs = [new WorkspaceTerminalTab("terminal-9", deletedId, "gone"), new WorkspaceTerminalTab("terminal-1", WorkspaceSamples.WebId, "web")],
            ActiveTerminalKey = "terminal-9",
            RemotePaths = [new WorkspaceRemotePath(deletedId, "/opt")],
            EditorTabs = [new WorkspaceEditorTab(deletedId, "/opt/app.conf")],
            ActiveEditorTab = new WorkspaceEditorTab(deletedId, "/opt/app.conf"),
        };
        var document = WorkspaceStateSerializer.Serialize(state);
        _store.LoadDocumentAsync(Arg.Any<CancellationToken>()).Returns(document);
        var loader = CreateLoader();

        var restore = await loader.LoadAsync(TestContext.CancellationToken);

        var restored = restore.Snapshot.State;
        restored.SelectedConnectionId.Should().BeNull();
        restored.TerminalTabs.Select(tab => tab.Key).Should().Equal("terminal-1");
        restored.ActiveTerminalKey.Should().BeNull();
        restored.RemotePaths.Should().BeEmpty();
        restored.EditorTabs.Should().BeEmpty();
        restored.ActiveEditorTab.Should().BeNull();
    }

    private WorkspaceStateLoader CreateLoader()
    {
        var upgrader = new WorkspaceStateUpgrader([]);
        var reader = new WorkspaceStateReader(upgrader);
        var filter = new WorkspaceConnectionFilter(_connections);
        return new WorkspaceStateLoader(_store, reader, filter);
    }
}
