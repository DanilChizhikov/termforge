using System.Net;
using System.Text.Json;
using DTech.Core.Terminal.Sessions;
using DTech.Entrypoint.Tests.Terminals.Support;
using FluentAssertions;

namespace DTech.Entrypoint.Tests.Terminals;

[TestClass]
public sealed class ReconnectEndpointsTests
{
    private const string ReconnectPath = "/api/terminals/reconnect";

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
    public async Task Reconnect_AfterRemoteShellExit_OpensAFreshShellWithoutReplayingInput()
    {
        var connectionId = await _api.CreateConnectionAsync("web");
        var first = await _api.OpenTerminalAsync(connectionId);
        using var firstSocket = await _api.AttachAsync(first.GetProperty("id").GetString()!);
        await firstSocket.ExpectMessageAsync("status");
        await firstSocket.SendInputAsync("rm -rf build\r");
        await firstSocket.ExpectOutputAsync("ECHO[rm -rf build\r]");
        _api.SessionFactory.Sessions[0].EndRemotely();
        await firstSocket.ExpectCloseAsync();

        var reconnected = await ReconnectAsync(connectionId);
        using var secondSocket = await _api.AttachAsync(reconnected.GetProperty("session").GetProperty("id").GetString()!);
        await secondSocket.ExpectMessageAsync("status");
        _api.SessionFactory.Sessions[1].Emit("fresh$ ");
        await secondSocket.ExpectOutputAsync("fresh$ ");

        reconnected.GetProperty("attempts").GetInt32().Should().Be(1);
        reconnected.GetProperty("session").GetProperty("id").GetString().Should().NotBe(first.GetProperty("id").GetString());
        _api.SessionFactory.Sessions.Should().HaveCount(2);
        _api.SessionFactory.Sessions[1].Input.Should().BeEmpty();
        secondSocket.Output.Should().NotContain("rm -rf build");
    }

    [TestMethod]
    public async Task Reconnect_WhenPolicyIsOff_IsRefused()
    {
        var connectionId = await _api.CreateConnectionAsync("web", new { reconnectMode = "Off" });

        using var response = await _api.SendAsync(HttpMethod.Post, ReconnectPath, new { connectionId, columns = 80, rows = 24 });
        var problem = await _api.ReadJsonAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        problem.GetProperty("code").GetString().Should().Be(nameof(TerminalErrorCode.ReconnectDisabled));
        _api.SessionFactory.Sessions.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Reconnect_ForUnknownConnection_ReturnsNotFound()
    {
        using var response = await _api.SendAsync(HttpMethod.Post, ReconnectPath, new { connectionId = Guid.NewGuid(), columns = 80, rows = 24 });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task Reconnect_WithoutConnectionId_IsRejected()
    {
        using var response = await _api.SendAsync(HttpMethod.Post, ReconnectPath, new { columns = 80, rows = 24 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task Reconnect_Automatic_WaitsForTheInitialBackoffThenOpensAShell()
    {
        var policy = new { reconnectMode = "Automatic", reconnectMaxAttempts = 2, reconnectInitialDelaySeconds = 1, reconnectMaxDelaySeconds = 2 };
        var connectionId = await _api.CreateConnectionAsync("web", policy);
        var started = DateTime.UtcNow;

        var reconnected = await ReconnectAsync(connectionId);

        (DateTime.UtcNow - started).Should().BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(900));
        reconnected.GetProperty("attempts").GetInt32().Should().Be(1);
        _api.SessionFactory.Sessions.Should().HaveCount(1);
    }

    [TestMethod]
    public async Task ConnectionApi_StoresAndReturnsTheReconnectPolicy()
    {
        var policy = new { reconnectMode = "Automatic", reconnectMaxAttempts = 7, reconnectInitialDelaySeconds = 3, reconnectMaxDelaySeconds = 120 };
        var connectionId = await _api.CreateConnectionAsync("web", policy);

        using var response = await _api.SendAsync(HttpMethod.Get, $"/api/connections/{connectionId}", null);
        var body = await _api.ReadJsonAsync(response);

        body.GetProperty("reconnectMode").GetString().Should().Be("Automatic");
        body.GetProperty("reconnectMaxAttempts").GetInt32().Should().Be(7);
        body.GetProperty("reconnectInitialDelaySeconds").GetInt32().Should().Be(3);
        body.GetProperty("reconnectMaxDelaySeconds").GetInt32().Should().Be(120);
    }

    [TestMethod]
    public async Task ConnectionApi_WithoutPolicy_DefaultsToManualReconnect()
    {
        var connectionId = await _api.CreateConnectionAsync("web");

        using var response = await _api.SendAsync(HttpMethod.Get, $"/api/connections/{connectionId}", null);
        var body = await _api.ReadJsonAsync(response);

        body.GetProperty("reconnectMode").GetString().Should().Be("Manual");
        body.GetProperty("reconnectMaxAttempts").GetInt32().Should().Be(5);
    }

    [TestMethod]
    [DataRow("Sometimes", 5, 2, 60)]
    [DataRow("Automatic", 0, 2, 60)]
    [DataRow("Automatic", 5, 30, 10)]
    public async Task ConnectionApi_RejectsInvalidReconnectPolicy(string mode, int attempts, int initial, int maximum)
    {
        var request = new Dictionary<string, object?>
        {
            ["name"] = "web",
            ["host"] = "web.example.test",
            ["userName"] = "deploy",
            ["reconnectMode"] = mode,
            ["reconnectMaxAttempts"] = attempts,
            ["reconnectInitialDelaySeconds"] = initial,
            ["reconnectMaxDelaySeconds"] = maximum,
        };

        using var response = await _api.SendAsync(HttpMethod.Post, "/api/connections", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<JsonElement> ReconnectAsync(Guid connectionId)
    {
        using var response = await _api.SendAsync(HttpMethod.Post, ReconnectPath, new { connectionId, columns = 80, rows = 24 });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(TestContext.CancellationToken));
        return await _api.ReadJsonAsync(response);
    }
}
