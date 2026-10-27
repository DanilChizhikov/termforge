using System.Net;
using System.Text.Json;
using DTech.Core.Workspace;
using DTech.Entrypoint.Tests.Terminals.Support;
using DTech.Persistence.Database;
using DTech.Persistence.Workspace;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace DTech.Entrypoint.Tests.Workspace;

[TestClass]
public sealed class WorkspaceEndpointsTests
{
    private const string WorkspacePath = "/api/workspace";

    private TerminalApiHarness _api = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public async Task Initialize()
    {
        _api = await TerminalApiHarness.StartAsync(TestContext);
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        await _api.DisposeAsync();
    }

    [TestMethod]
    public async Task Get_WithoutSavedState_ReturnsAnEmptyWorkspace()
    {
        using var response = await _api.SendAsync(HttpMethod.Get, WorkspacePath, null);
        var body = await _api.ReadJsonAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("state").GetProperty("terminalTabs").GetArrayLength().Should().Be(0);
        body.GetProperty("drafts").GetArrayLength().Should().Be(0);
        body.GetProperty("warning").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [TestMethod]
    public async Task Put_ThenGet_RestoresTheWorkspaceWithUnsavedDrafts()
    {
        var connectionId = await _api.CreateConnectionAsync("web");
        var snapshot = CreateSnapshot(connectionId);

        using var saved = await _api.SendAsync(HttpMethod.Put, WorkspacePath, snapshot);
        using var restored = await _api.SendAsync(HttpMethod.Get, WorkspacePath, null);
        var body = await _api.ReadJsonAsync(restored);

        saved.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var state = body.GetProperty("state");
        state.GetProperty("selectedConnectionId").GetGuid().Should().Be(connectionId);
        state.GetProperty("activeView").GetString().Should().Be("editor");
        state.GetProperty("localPath").GetString().Should().Be("/home/dev");
        state.GetProperty("terminalTabs")[0].GetProperty("connectionName").GetString().Should().Be("web");
        state.GetProperty("remotePaths")[0].GetProperty("path").GetString().Should().Be("/var/www");
        state.GetProperty("panelLayouts")[0].GetProperty("sizes").GetProperty("main").GetDouble().Should().Be(70);
        state.GetProperty("collapsedPanels")[0].GetString().Should().Be("local-files");
        state.GetProperty("activeEditorTab").GetProperty("path").GetString().Should().Be("/etc/hosts");
        var draft = body.GetProperty("drafts").EnumerateArray().Should().ContainSingle().Subject;
        draft.GetProperty("content").GetString().Should().Be("127.0.0.1 web");
        draft.GetProperty("baseVersion").GetProperty("sha256").GetString().Should().Be("abc");
    }

    [TestMethod]
    public async Task Put_InvalidState_IsRejected()
    {
        var invalid = new { state = new { collapsedPanels = new[] { "../escape" } }, drafts = Array.Empty<object>() };

        using var response = await _api.SendAsync(HttpMethod.Put, WorkspacePath, invalid);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task Get_CorruptedSavedState_ResetsItAndReportsAWarningOnce()
    {
        var corrupted = new StoredWorkspaceDocument(WorkspaceStateSchema.CurrentVersion, "{broken");
        await WriteRawDocumentAsync(corrupted);

        using var first = await _api.SendAsync(HttpMethod.Get, WorkspacePath, null);
        var firstBody = await _api.ReadJsonAsync(first);
        using var second = await _api.SendAsync(HttpMethod.Get, WorkspacePath, null);
        var secondBody = await _api.ReadJsonAsync(second);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        firstBody.GetProperty("warning").GetProperty("code").GetString().Should().Be("Corrupted");
        firstBody.GetProperty("warning").GetProperty("message").GetString().Should().Contain("reset");
        secondBody.GetProperty("warning").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [TestMethod]
    public async Task Flush_AfterSave_Completes()
    {
        var connectionId = await _api.CreateConnectionAsync("web");
        var snapshot = CreateSnapshot(connectionId);
        using var saved = await _api.SendAsync(HttpMethod.Put, WorkspacePath, snapshot);
        var coordinator = _api.Factory.Services.GetRequiredService<WorkspaceStateWriteCoordinator>();

        var flush = coordinator.FlushAsync(TestContext.CancellationToken);
        await flush;

        flush.IsCompletedSuccessfully.Should().BeTrue();
    }

    private static object CreateSnapshot(Guid connectionId)
    {
        var editorTab = new { connectionId, path = "/etc/hosts" };
        var state = new
        {
            selectedConnectionId = connectionId,
            activeView = "editor",
            terminalTabs = new[] { new { key = "terminal-1", connectionId, connectionName = "web" } },
            activeTerminalKey = "terminal-1",
            localPath = "/home/dev",
            remotePaths = new[] { new { connectionId, path = "/var/www" } },
            panelLayouts = new[] { new { groupId = "app-layout", sizes = new Dictionary<string, double> { ["servers"] = 30, ["main"] = 70 } } },
            collapsedPanels = new[] { "local-files" },
            editorTabs = new[] { editorTab },
            activeEditorTab = editorTab,
        };
        var baseVersion = new { modifiedUtc = "2026-03-04T05:06:07Z", size = 12, sha256 = "abc" };
        var drafts = new[] { new { connectionId, path = "/etc/hosts", content = "127.0.0.1 web", baseVersion } };
        return new { state, drafts };
    }

    private async Task WriteRawDocumentAsync(StoredWorkspaceDocument document)
    {
        using var scope = _api.Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TermForgeDbContext>();
        var store = new WorkspaceStateStore(context, TimeProvider.System);
        await store.SaveAsync(document, [], TestContext.CancellationToken);
    }
}
