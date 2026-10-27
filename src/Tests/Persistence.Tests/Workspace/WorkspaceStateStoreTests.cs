using DTech.Core.Editor;
using DTech.Core.SavedConnections;
using DTech.Core.Workspace;
using DTech.Persistence.SavedConnections;
using DTech.Persistence.Tests.Infrastructure;
using DTech.Persistence.Workspace;
using FluentAssertions;

namespace DTech.Persistence.Tests.Workspace;

[TestClass]
public sealed class WorkspaceStateStoreTests
{
    private TemporaryDatabase _database = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public async Task Initialize()
    {
        _database = new TemporaryDatabase();
        await _database.MigrateAsync();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _database.Dispose();
    }

    [TestMethod]
    public async Task Load_WithoutSavedState_ReturnsNothing()
    {
        await using var context = _database.CreateContext();
        var store = new WorkspaceStateStore(context, TimeProvider.System);

        var document = await store.LoadDocumentAsync(TestContext.CancellationToken);
        var drafts = await store.LoadDraftsAsync(TestContext.CancellationToken);

        document.Should().BeNull();
        drafts.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Save_ThenLoad_RoundTripsDocumentAndDraftsAndReplacesThePreviousSave()
    {
        var connectionId = await AddConnectionAsync("web");
        var modifiedUtc = new DateTime(2026, 2, 3, 4, 5, 6, DateTimeKind.Utc);
        var baseVersion = new RemoteFileVersion(modifiedUtc, 42, "deadbeef");
        var firstDraft = new WorkspaceEditorDraft(connectionId, "/etc/hosts", "127.0.0.1 web", baseVersion);
        var secondDraft = new WorkspaceEditorDraft(connectionId, "/etc/motd", "hello", null);
        var first = new StoredWorkspaceDocument(1, "{\"localPath\":\"/first\"}");
        var second = new StoredWorkspaceDocument(1, "{\"localPath\":\"/second\"}");

        await using (var context = _database.CreateContext())
        {
            var store = new WorkspaceStateStore(context, TimeProvider.System);
            await store.SaveAsync(first, [firstDraft], TestContext.CancellationToken);
            await store.SaveAsync(second, [secondDraft], TestContext.CancellationToken);
        }

        await using var reloadContext = _database.CreateContext();
        var reloadStore = new WorkspaceStateStore(reloadContext, TimeProvider.System);
        var document = await reloadStore.LoadDocumentAsync(TestContext.CancellationToken);
        var drafts = await reloadStore.LoadDraftsAsync(TestContext.CancellationToken);
        document.Should().Be(second);
        drafts.Should().Equal(secondDraft);
    }

    [TestMethod]
    public async Task Save_DraftBaseVersion_KeepsUtcTimestamp()
    {
        var connectionId = await AddConnectionAsync("web");
        var modifiedUtc = new DateTime(2026, 2, 3, 4, 5, 6, DateTimeKind.Utc);
        var baseVersion = new RemoteFileVersion(modifiedUtc, 42, "deadbeef");
        var draft = new WorkspaceEditorDraft(connectionId, "/etc/hosts", "127.0.0.1 web", baseVersion);
        var document = new StoredWorkspaceDocument(1, "{}");

        await using (var context = _database.CreateContext())
        {
            var store = new WorkspaceStateStore(context, TimeProvider.System);
            await store.SaveAsync(document, [draft], TestContext.CancellationToken);
        }

        await using var reloadContext = _database.CreateContext();
        var reloadStore = new WorkspaceStateStore(reloadContext, TimeProvider.System);
        var drafts = await reloadStore.LoadDraftsAsync(TestContext.CancellationToken);
        drafts.Should().Equal(draft);
        drafts[0].BaseVersion!.ModifiedUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    [TestMethod]
    public async Task DeleteDocument_KeepsUnsavedDrafts()
    {
        var connectionId = await AddConnectionAsync("web");
        var draft = new WorkspaceEditorDraft(connectionId, "/etc/hosts", "edited", null);
        var document = new StoredWorkspaceDocument(1, "{}");
        await using var context = _database.CreateContext();
        var store = new WorkspaceStateStore(context, TimeProvider.System);
        await store.SaveAsync(document, [draft], TestContext.CancellationToken);

        await store.DeleteDocumentAsync(TestContext.CancellationToken);

        var remainingDocument = await store.LoadDocumentAsync(TestContext.CancellationToken);
        var remainingDrafts = await store.LoadDraftsAsync(TestContext.CancellationToken);
        remainingDocument.Should().BeNull();
        remainingDrafts.Should().Equal(draft);
    }

    [TestMethod]
    public async Task DeletingAServer_RemovesItsDrafts()
    {
        var connectionId = await AddConnectionAsync("web");
        var draft = new WorkspaceEditorDraft(connectionId, "/etc/hosts", "edited", null);
        var document = new StoredWorkspaceDocument(1, "{}");
        await using (var context = _database.CreateContext())
        {
            var store = new WorkspaceStateStore(context, TimeProvider.System);
            await store.SaveAsync(document, [draft], TestContext.CancellationToken);
            var repository = new SavedConnectionRepository(context);
            await repository.DeleteAsync(connectionId, TestContext.CancellationToken);
        }

        var rows = _database.Query("SELECT * FROM workspace_editor_drafts");
        rows.Should().BeEmpty();
    }

    private async Task<Guid> AddConnectionAsync(string name)
    {
        var details = new SavedConnectionDraft { Name = name, Host = "example.org", UserName = "deploy" };
        var connection = new SavedConnection { Id = Guid.NewGuid(), Details = details };
        await using var context = _database.CreateContext();
        var repository = new SavedConnectionRepository(context);
        await repository.AddAsync(connection, TestContext.CancellationToken);
        return connection.Id;
    }
}
