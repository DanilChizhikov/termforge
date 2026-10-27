using DTech.Core.SavedConnections;
using DTech.Core.Workspace;
using FluentAssertions;
using NSubstitute;

namespace DTech.Core.Tests.Workspace;

[TestClass]
public sealed class WorkspaceStateSaverTests
{
    private readonly IWorkspaceStateStore _store = Substitute.For<IWorkspaceStateStore>();
    private readonly ISavedConnectionRepository _connections = Substitute.For<ISavedConnectionRepository>();
    private readonly WorkspaceStateWriteCoordinator _coordinator = new();

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Initialize()
    {
        var web = WorkspaceSamples.Connection(WorkspaceSamples.WebId, "web");
        var database = WorkspaceSamples.Connection(WorkspaceSamples.DatabaseId, "db");
        IReadOnlyList<SavedConnection> connections = [web, database];
        _connections.ListAsync(Arg.Any<CancellationToken>()).Returns(connections);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _coordinator.Dispose();
    }

    [TestMethod]
    public async Task Save_ValidSnapshot_StoresTheCurrentSchemaVersionAndDrafts()
    {
        var draft = WorkspaceSamples.Draft(WorkspaceSamples.WebId, "/etc/nginx/nginx.conf");
        var snapshot = new WorkspaceSnapshot(WorkspaceSamples.State(), [draft]);
        var saver = CreateSaver();

        var saved = await saver.SaveAsync(snapshot, TestContext.CancellationToken);

        saved.IsSuccess.Should().BeTrue();
        await _store.Received(1).SaveAsync(
            Arg.Is<StoredWorkspaceDocument>(document => document.SchemaVersion == WorkspaceStateSchema.CurrentVersion),
            Arg.Is<IReadOnlyList<WorkspaceEditorDraft>>(drafts => drafts.Single() == draft),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task Save_InvalidSnapshot_IsRejectedWithoutWriting()
    {
        var state = WorkspaceSamples.State() with { CollapsedPanels = ["bad id!"] };
        var snapshot = new WorkspaceSnapshot(state, []);
        var saver = CreateSaver();

        var saved = await saver.SaveAsync(snapshot, TestContext.CancellationToken);

        saved.IsSuccess.Should().BeFalse();
        await _store.DidNotReceiveWithAnyArgs().SaveAsync(default!, default!, default);
    }

    [TestMethod]
    public async Task Save_DraftForADeletedServer_IsDropped()
    {
        var deletedId = Guid.NewGuid();
        var draft = WorkspaceSamples.Draft(deletedId, "/opt/app.conf");
        var state = WorkspaceSamples.State() with { EditorTabs = [draft.ToTab()], ActiveEditorTab = null };
        var snapshot = new WorkspaceSnapshot(state, [draft]);
        var saver = CreateSaver();

        await saver.SaveAsync(snapshot, TestContext.CancellationToken);

        await _store.Received(1).SaveAsync(
            Arg.Any<StoredWorkspaceDocument>(),
            Arg.Is<IReadOnlyList<WorkspaceEditorDraft>>(drafts => drafts.Count == 0),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task Flush_WaitsForTheWriteInProgress()
    {
        var writeStarted = new TaskCompletionSource();
        var releaseWrite = new TaskCompletionSource();
        var write = _coordinator.RunAsync(
            async _ =>
            {
                writeStarted.SetResult();
                await releaseWrite.Task;
            },
            TestContext.CancellationToken);
        await writeStarted.Task;

        var flush = _coordinator.FlushAsync(TestContext.CancellationToken);
        flush.IsCompleted.Should().BeFalse();
        releaseWrite.SetResult();
        await write;
        await flush;

        flush.IsCompletedSuccessfully.Should().BeTrue();
    }

    private WorkspaceStateSaver CreateSaver()
    {
        var filter = new WorkspaceConnectionFilter(_connections);
        return new WorkspaceStateSaver(_store, filter, _coordinator);
    }
}
