using System.Net;
using DTech.Entrypoint.Tests.Terminals.Support;
using FluentAssertions;

namespace DTech.Entrypoint.Tests.Safety;

[TestClass]
public sealed class ReadOnlyEndpointsTests
{
    private const string FilePath = "/srv/app/.env";

    private TerminalApiHarness _harness = null!;
    private Guid _connectionId;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public async Task Initialize()
    {
        _harness = await TerminalApiHarness.StartAsync(TestContext);
        var readOnly = new { readOnly = true };
        _connectionId = await _harness.CreateConnectionAsync("db-01", readOnly);
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        await _harness.DisposeAsync();
    }

    [TestMethod]
    public async Task Connection_ExposesReadOnlyFlag()
    {
        using var response = await _harness.SendAsync(HttpMethod.Get, $"/api/connections/{_connectionId}", null);

        var body = await _harness.ReadJsonAsync(response);
        body.GetProperty("readOnly").GetBoolean().Should().BeTrue();
    }

    [TestMethod]
    public async Task CreateDirectory_IsLocked()
    {
        var body = new { parentPath = "/srv", name = "new" };

        await AssertLockedAsync(HttpMethod.Post, $"/api/files/remote/{_connectionId}/directories", body);
    }

    [TestMethod]
    public async Task Rename_IsLocked()
    {
        var body = new { path = FilePath, newName = "renamed" };

        await AssertLockedAsync(HttpMethod.Post, $"/api/files/remote/{_connectionId}/rename", body);
    }

    [TestMethod]
    public async Task Delete_IsLocked()
    {
        var body = new { path = FilePath, confirmationToken = "any" };

        await AssertLockedAsync(HttpMethod.Post, $"/api/files/remote/{_connectionId}/delete", body);
    }

    [TestMethod]
    public async Task Upload_IsLocked()
    {
        var body = new { connectionId = _connectionId, direction = "upload", sourcePaths = new[] { "/tmp/file.txt" }, targetDirectory = "/srv", overwrite = false };

        await AssertLockedAsync(HttpMethod.Post, "/api/transfers", body);
    }

    [TestMethod]
    public async Task Save_IsLocked()
    {
        var body = new { path = FilePath, content = "A=2\n", hasByteOrderMark = false };

        await AssertLockedAsync(HttpMethod.Put, $"/api/editor/remote/{_connectionId}/file", body);
    }

    [TestMethod]
    public async Task Rollback_IsLocked()
    {
        var body = new { path = FilePath, backupName = "app.env.bak", confirmationToken = "any" };

        await AssertLockedAsync(HttpMethod.Post, $"/api/editor/remote/{_connectionId}/rollback", body);
    }

    [TestMethod]
    public async Task PrivilegedSave_IsLocked()
    {
        var body = new { path = FilePath, content = "A=2\n", hasByteOrderMark = false, sudo = new { password = "pw" } };

        await AssertLockedAsync(HttpMethod.Put, $"/api/editor/remote/{_connectionId}/sudo/file", body);
    }

    [TestMethod]
    public async Task PrivilegedRollback_IsLocked()
    {
        var body = new { path = FilePath, backupName = "app.env.bak", sudo = new { password = "pw" }, confirmationToken = "any" };

        await AssertLockedAsync(HttpMethod.Post, $"/api/editor/remote/{_connectionId}/sudo/rollback", body);
    }

    [TestMethod]
    [DataRow("start")]
    [DataRow("stop")]
    [DataRow("restart")]
    public async Task DockerAction_IsLocked(string action)
    {
        var body = new { confirmationToken = "any" };

        await AssertLockedAsync(HttpMethod.Post, $"/api/connections/{_connectionId}/docker/containers/web/{action}", body);
    }

    [TestMethod]
    public async Task DockerShell_IsLocked()
    {
        var body = new { columns = 100, rows = 30, shell = "auto" };

        await AssertLockedAsync(HttpMethod.Post, $"/api/connections/{_connectionId}/docker/containers/web/shell", body);
        _harness.SessionFactory.Sessions.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ConfirmationToken_IsNotIssued()
    {
        var body = new { operation = "RemoteDelete", connectionId = _connectionId, target = FilePath };

        await AssertLockedAsync(HttpMethod.Post, "/api/confirmations", body);
    }

    [TestMethod]
    public async Task Terminal_StaysAvailable()
    {
        var terminal = await _harness.OpenTerminalAsync(_connectionId);

        terminal.GetProperty("connectionId").GetGuid().Should().Be(_connectionId);
    }

    [TestMethod]
    public async Task ClearingFlag_UnlocksWrites()
    {
        var update = new { name = "db-01", host = "db-01.example.test", port = 22, userName = "deploy", authMethod = "Agent", readOnly = false };
        using var updated = await _harness.SendAsync(HttpMethod.Put, $"/api/connections/{_connectionId}", update);
        updated.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = new { operation = "RemoteDelete", connectionId = _connectionId, target = FilePath };

        using var response = await _harness.SendAsync(HttpMethod.Post, "/api/confirmations", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task AssertLockedAsync(HttpMethod method, string path, object body)
    {
        using var response = await _harness.SendAsync(method, path, body);

        var text = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Locked, text);
        var json = await _harness.ReadJsonAsync(response);
        json.GetProperty("code").GetString().Should().Be("ReadOnlyServer");
    }
}
