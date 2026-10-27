using System.Net;
using System.Text.Json;
using DTech.Entrypoint.Tests.Infrastructure;
using DTech.Entrypoint.Tests.Terminals.Support;
using FluentAssertions;

namespace DTech.Entrypoint.Tests.Recordings;

[TestClass]
public sealed class TerminalRecordingEndpointsTests
{
    private const string SettingsPath = "/api/recordings/settings";
    private const string RecordingsPath = "/api/recordings";

    private TerminalApiHarness _harness = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public async Task Initialize()
    {
        _harness = await TerminalApiHarness.StartAsync(TestContext);
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        await _harness.DisposeAsync();
    }

    [TestMethod]
    public async Task Settings_AreOffByDefaultAndStoredInTheAppDataDirectory()
    {
        using var response = await _harness.SendAsync(HttpMethod.Get, SettingsPath, null);
        var settings = await _harness.ReadJsonAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        settings.GetProperty("enabled").GetBoolean().Should().BeFalse();
        settings.GetProperty("maxRecordingSizeMegabytes").GetInt32().Should().Be(10);
        settings.GetProperty("maxRecordings").GetInt32().Should().Be(50);
        settings.GetProperty("retentionDays").GetInt32().Should().Be(30);
        settings.GetProperty("directory").GetString().Should().EndWith("recordings");
    }

    [TestMethod]
    public async Task StartRecording_WhileRecordingIsOff_IsRejected()
    {
        var terminalId = await OpenAttachedTerminalAsync();

        using var response = await _harness.SendAsync(HttpMethod.Post, RecordingPath(terminalId.Id), null);
        var problem = await _harness.ReadJsonAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        problem.GetProperty("code").GetString().Should().Be("Disabled");
        terminalId.Socket.Dispose();
    }

    [TestMethod]
    public async Task UpdateSettings_OutsideTheLimits_IsRejected()
    {
        var invalid = new { enabled = true, maxRecordingSizeMegabytes = 0, maxRecordings = 50, retentionDays = 30 };

        using var response = await _harness.SendAsync(HttpMethod.Put, SettingsPath, invalid);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task RecordedSession_CanBeListedReplayedAndDeleted()
    {
        await EnableRecordingAsync();
        var terminal = await OpenAttachedTerminalAsync();

        using var started = await _harness.SendAsync(HttpMethod.Post, RecordingPath(terminal.Id), null);
        var startedBody = await _harness.ReadJsonAsync(started);
        started.StatusCode.Should().Be(HttpStatusCode.OK);
        startedBody.GetProperty("recording").GetBoolean().Should().BeTrue();
        var recordingId = startedBody.GetProperty("recordingId").GetString()!;

        _harness.SessionFactory.Sessions[0].Emit("visible-output");
        await terminal.Socket.ExpectOutputAsync("visible-output");
        await WaitForRecordedBytesAsync(terminal.Id, startedBody.GetProperty("sizeBytes").GetInt64());
        using var stopped = await _harness.SendAsync(HttpMethod.Delete, RecordingPath(terminal.Id), null);
        var stoppedBody = await _harness.ReadJsonAsync(stopped);
        stoppedBody.GetProperty("recording").GetBoolean().Should().BeFalse();
        stoppedBody.GetProperty("status").GetString().Should().Be("complete");

        using var listed = await _harness.SendAsync(HttpMethod.Get, RecordingsPath, null);
        var list = await _harness.ReadJsonAsync(listed);
        list.GetArrayLength().Should().Be(1);
        list[0].GetProperty("id").GetString().Should().Be(recordingId);
        list[0].GetProperty("connectionName").GetString().Should().Be("web");
        list[0].GetProperty("status").GetString().Should().Be("complete");

        using var cast = await _harness.SendAsync(HttpMethod.Get, $"{RecordingsPath}/{recordingId}/cast", null);
        var castText = await cast.Content.ReadAsStringAsync(TestContext.CancellationToken);
        cast.Content.Headers.ContentType!.MediaType.Should().Be("application/x-asciicast");
        var lines = castText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        using (var header = JsonDocument.Parse(lines[0]))
        {
            header.RootElement.GetProperty("version").GetInt32().Should().Be(2);
            header.RootElement.GetProperty("width").GetInt32().Should().Be(80);
        }

        castText.Should().Contain("visible-output");

        using var deleted = await _harness.SendAsync(HttpMethod.Delete, $"{RecordingsPath}/{recordingId}", null);
        using var afterDelete = await _harness.SendAsync(HttpMethod.Get, RecordingsPath, null);
        var remaining = await _harness.ReadJsonAsync(afterDelete);
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        remaining.GetArrayLength().Should().Be(0);
        terminal.Socket.Dispose();
    }

    [TestMethod]
    public async Task RecordingStatus_ShowsWhetherRecordingIsAvailable()
    {
        var terminal = await OpenAttachedTerminalAsync();

        using var before = await _harness.SendAsync(HttpMethod.Get, RecordingPath(terminal.Id), null);
        var beforeBody = await _harness.ReadJsonAsync(before);
        await EnableRecordingAsync();
        using var after = await _harness.SendAsync(HttpMethod.Get, RecordingPath(terminal.Id), null);
        var afterBody = await _harness.ReadJsonAsync(after);

        beforeBody.GetProperty("recording").GetBoolean().Should().BeFalse();
        beforeBody.GetProperty("available").GetBoolean().Should().BeFalse();
        afterBody.GetProperty("available").GetBoolean().Should().BeTrue();
        afterBody.GetProperty("recording").GetBoolean().Should().BeFalse();
        terminal.Socket.Dispose();
    }

    [TestMethod]
    [DataRow("not-a-recording-id")]
    [DataRow("..%2F..%2Ftermforge.db")]
    public async Task RecordingEndpoints_RejectIdsThatAreNotRecordingIds(string id)
    {
        using var cast = await _harness.SendAsync(HttpMethod.Get, $"{RecordingsPath}/{id}/cast", null);
        using var deleted = await _harness.SendAsync(HttpMethod.Delete, $"{RecordingsPath}/{id}", null);

        cast.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
        deleted.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
        cast.Content.Headers.ContentType?.MediaType.Should().NotBe("application/x-asciicast");
    }

    [TestMethod]
    public async Task RecordingEndpoints_RequireTheBrowserSession()
    {
        using var client = _harness.Factory.CreateClientWithoutCookies();
        using var request = new HttpRequestMessage(HttpMethod.Get, RecordingsPath);
        request.Headers.Add("Origin", TermForgeApplicationFactory.ApplicationOrigin);

        using var response = await client.SendAsync(request, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static string RecordingPath(string terminalId)
    {
        return $"/api/terminals/{terminalId}/recording";
    }

    private async Task EnableRecordingAsync()
    {
        var enabled = new { enabled = true, maxRecordingSizeMegabytes = 10, maxRecordings = 50, retentionDays = 30 };
        using var response = await _harness.SendAsync(HttpMethod.Put, SettingsPath, enabled);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<AttachedTerminal> OpenAttachedTerminalAsync()
    {
        var connectionId = await _harness.CreateConnectionAsync("web");
        var opened = await _harness.OpenTerminalAsync(connectionId);
        var id = opened.GetProperty("id").GetString()!;
        var socket = await _harness.AttachAsync(id);
        await socket.ExpectMessageAsync("status");
        return new AttachedTerminal(id, socket);
    }

    private async Task WaitForRecordedBytesAsync(string terminalId, long headerSize)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        while (true)
        {
            using var status = await _harness.SendAsync(HttpMethod.Get, RecordingPath(terminalId), null);
            var body = await _harness.ReadJsonAsync(status);
            if (body.GetProperty("sizeBytes").GetInt64() > headerSize)
            {
                return;
            }

            await Task.Delay(20, timeout.Token);
        }
    }

    private sealed record AttachedTerminal(string Id, TerminalSocket Socket);
}
