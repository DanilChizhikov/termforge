using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text.Json;
using DTech.Core.Connections;
using DTech.Core.Diagnostics;
using DTech.Core.Terminal;
using DTech.Entrypoint.Tests.Docker.Support;
using DTech.Entrypoint.Tests.Infrastructure;
using DTech.Entrypoint.Tests.Terminals.Support;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace DTech.Entrypoint.Tests.Docker;

[TestClass]
public sealed class DockerEndpointsTests
{
    private const string ContainerList =
        "{\"ID\":\"3f4e1b2c9d8a\",\"Image\":\"nginx:1.27-alpine\",\"Names\":\"web\",\"Ports\":\"0.0.0.0:8080->80/tcp\",\"State\":\"running\",\"Status\":\"Up 6 days (healthy)\",\"CreatedAt\":\"2026-10-20 09:12:44 +0000 UTC\"}\n"
        + "{\"ID\":\"a1b2c3d4e5f6\",\"Image\":\"postgres:17\",\"Names\":\"db\",\"Ports\":\"\",\"State\":\"exited\",\"Status\":\"Exited (0) 2 hours ago\",\"CreatedAt\":\"2026-10-19 18:01:02 +0000 UTC\"}\n";

    private const string PermissionDenied = "permission denied while trying to connect to the Docker daemon socket at unix:///var/run/docker.sock\n";

    private ScriptedDockerRunner _runner = null!;
    private IRemoteCommandStreamFactory _streamFactory = null!;
    private ScriptedTerminalSessionFactory _terminalFactory = null!;
    private TermForgeApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private AuthenticatedSession _session = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public async Task Initialize()
    {
        _runner = new ScriptedDockerRunner();
        _streamFactory = Substitute.For<IRemoteCommandStreamFactory>();
        _terminalFactory = new ScriptedTerminalSessionFactory();
        _factory = new TermForgeApplicationFactory { ConfigureServices = ReplaceServices };
        _client = _factory.CreateClientWithoutCookies();
        _session = await SessionBootstrapper.AuthenticateAsync(_factory, _client, TestContext.CancellationToken);
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [TestMethod]
    public async Task Overview_WithoutSession_IsUnauthorized()
    {
        using var anonymous = _factory.CreateClientWithoutCookies();
        var path = $"/api/connections/{Guid.NewGuid()}/docker";

        using var response = await TestRequest.Get(path).SendAsync(anonymous, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task Overview_ListsContainersWithAvailability()
    {
        _runner.Respond("command -v docker", 0, "/usr/bin/docker\n")
            .Respond("docker info", 0, "27.3.1\n")
            .Respond("docker ps", 0, ContainerList);
        var id = await CreateConnectionAsync();

        using var response = await SendAsync(HttpMethod.Get, $"/api/connections/{id}/docker", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(response);
        body.GetProperty("serverName").GetString().Should().Be("web");
        body.GetProperty("availability").GetProperty("state").GetString().Should().Be("Available");
        var containers = body.GetProperty("containers");
        containers.GetArrayLength().Should().Be(2);
        containers[0].GetProperty("name").GetString().Should().Be("web");
        containers[0].GetProperty("state").GetString().Should().Be("Running");
        containers[0].GetProperty("health").GetString().Should().Be("healthy");
        containers[1].GetProperty("state").GetString().Should().Be("Exited");
    }

    [TestMethod]
    public async Task Overview_WhenDockerMissing_ReportsNotInstalled()
    {
        _runner.Respond("command -v docker", 127, string.Empty);
        var id = await CreateConnectionAsync();

        using var response = await SendAsync(HttpMethod.Get, $"/api/connections/{id}/docker", null);

        var body = await ReadJsonAsync(response);
        var availability = body.GetProperty("availability");
        availability.GetProperty("state").GetString().Should().Be("NotInstalled");
        availability.GetProperty("reason").GetString().Should().Contain("not installed");
        body.GetProperty("containers").GetArrayLength().Should().Be(0);
    }

    [TestMethod]
    public async Task Overview_WhenPermissionDenied_ReportsNoAccess()
    {
        _runner.Respond("command -v docker", 0, "/usr/bin/docker\n")
            .Respond("docker info", 1, PermissionDenied);
        var id = await CreateConnectionAsync();

        using var response = await SendAsync(HttpMethod.Get, $"/api/connections/{id}/docker", null);

        var body = await ReadJsonAsync(response);
        body.GetProperty("availability").GetProperty("state").GetString().Should().Be("NoAccess");
        body.GetProperty("availability").GetProperty("reason").GetString().Should().Contain("docker group");
    }

    [TestMethod]
    public async Task Stop_WithoutConfirmation_IsConflictAndRunsNothing()
    {
        var id = await CreateConnectionAsync();

        using var response = await SendAsync(HttpMethod.Post, $"/api/connections/{id}/docker/containers/web/stop", new { confirmationToken = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await ReadJsonAsync(response);
        body.GetProperty("code").GetString().Should().Be("ConfirmationRequired");
        _runner.ExecutedCommands.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Restart_WithConfirmation_RunsAndAppearsInJournal()
    {
        _runner.Respond("docker restart", 0, "web\n");
        var id = await CreateConnectionAsync();
        var token = await RequestConfirmationAsync("ContainerRestart", id, "web");

        using var response = await SendAsync(HttpMethod.Post, $"/api/connections/{id}/docker/containers/web/restart", new { confirmationToken = token });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(response);
        body.GetProperty("action").GetString().Should().Be("restart");
        var operationId = body.GetProperty("operationId").GetGuid();
        _runner.ExecutedCommands.Should().Contain("docker restart 'web'");
        using var operation = await SendAsync(HttpMethod.Get, $"/api/operations/{operationId}", null);
        operation.StatusCode.Should().Be(HttpStatusCode.OK);
        var journal = await ReadJsonAsync(operation);
        journal.GetProperty("kind").GetString().Should().Be("docker.restart");
        journal.GetProperty("status").GetString().Should().Be("succeeded");
    }

    [TestMethod]
    public async Task Stop_ReusingConfirmationToken_IsConflictTheSecondTime()
    {
        _runner.Respond("docker stop", 0, "web\n");
        var id = await CreateConnectionAsync();
        var token = await RequestConfirmationAsync("ContainerStop", id, "web");
        var body = new { confirmationToken = token };

        using var first = await SendAsync(HttpMethod.Post, $"/api/connections/{id}/docker/containers/web/stop", body);
        using var replay = await SendAsync(HttpMethod.Post, $"/api/connections/{id}/docker/containers/web/stop", body);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        replay.StatusCode.Should().Be(HttpStatusCode.Conflict);
        _runner.ExecutedCommands.Should().ContainSingle();
    }

    [TestMethod]
    public async Task Stop_WithTokenForOtherContainer_IsConflict()
    {
        var id = await CreateConnectionAsync();
        var token = await RequestConfirmationAsync("ContainerStop", id, "db");

        using var response = await SendAsync(HttpMethod.Post, $"/api/connections/{id}/docker/containers/web/stop", new { confirmationToken = token });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        _runner.ExecutedCommands.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Confirmation_DescribesServerAndTarget()
    {
        var id = await CreateConnectionAsync();
        var request = new { operation = "ContainerStop", connectionId = id, target = "web" };

        using var response = await SendAsync(HttpMethod.Post, "/api/confirmations", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(response);
        body.GetProperty("serverName").GetString().Should().Be("web");
        body.GetProperty("target").GetString().Should().Be("web");
        body.GetProperty("operation").GetString().Should().Be("ContainerStop");
        body.GetProperty("token").GetString().Should().NotBeNullOrEmpty();
    }

    [TestMethod]
    [DataRow("Reboot")]
    [DataRow("1")]
    [DataRow(null)]
    public async Task Confirmation_UnknownOperation_IsBadRequest(string? operation)
    {
        var id = await CreateConnectionAsync();
        var request = new { operation, connectionId = id, target = "web" };

        using var response = await SendAsync(HttpMethod.Post, "/api/confirmations", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task Confirmation_UnknownConnection_IsNotFound()
    {
        var request = new { operation = "ContainerStop", connectionId = Guid.NewGuid(), target = "web" };

        using var response = await SendAsync(HttpMethod.Post, "/api/confirmations", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [TestMethod]
    [DataRow("start", "web;reboot")]
    [DataRow("remove", "web")]
    [DataRow("1", "web")]
    public async Task Action_WithInvalidInput_IsBadRequest(string action, string container)
    {
        var id = await CreateConnectionAsync();
        var encodedContainer = Uri.EscapeDataString(container);

        using var response = await SendAsync(HttpMethod.Post, $"/api/connections/{id}/docker/containers/{encodedContainer}/{action}", new { confirmationToken = "any" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _runner.ExecutedCommands.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Logs_StreamsOutputUntilCommandEnds()
    {
        var stream = new ChannelCommandStream();
        var started = SshResult<IRemoteCommandStream>.Success(stream);
        _streamFactory.StartAsync(default!, default!, default).ReturnsForAnyArgs(started);
        var id = await CreateConnectionAsync();
        using var socket = await ConnectLogsAsync($"/api/connections/{id}/docker/containers/web/logs/ws?tail=20&follow=true");

        stream.Emit("line one\n");
        var first = await socket.ExpectMessageAsync("output");
        stream.Emit("line two\n");
        stream.Complete(0);
        var end = await socket.ExpectMessageAsync("end");

        first.GetProperty("text").GetString().Should().Be("line one\n");
        end.GetProperty("exitCode").GetInt32().Should().Be(0);
        await _streamFactory.Received(1).StartAsync(Arg.Any<SshConnectionTarget>(), "docker logs --tail 20 --follow 'web' 2>&1", Arg.Any<CancellationToken>());
        await stream.Disposed.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task Logs_WhenViewerCloses_StopsRemoteCommand()
    {
        var stream = new ChannelCommandStream();
        var started = SshResult<IRemoteCommandStream>.Success(stream);
        _streamFactory.StartAsync(default!, default!, default).ReturnsForAnyArgs(started);
        var id = await CreateConnectionAsync();
        using var socket = await ConnectLogsAsync($"/api/connections/{id}/docker/containers/web/logs/ws?follow=true");
        stream.Emit("hello\n");
        await socket.ExpectMessageAsync("output");

        await socket.WebSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "done", TestContext.CancellationToken);

        await stream.Disposed.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task Logs_WithUnsafeContainer_SendsErrorWithoutStarting()
    {
        var id = await CreateConnectionAsync();
        var encodedContainer = Uri.EscapeDataString("web$(id)");
        using var socket = await ConnectLogsAsync($"/api/connections/{id}/docker/containers/{encodedContainer}/logs/ws");

        var error = await socket.ExpectMessageAsync("error");

        error.GetProperty("message").GetString().Should().Contain("invalid");
        await _streamFactory.DidNotReceiveWithAnyArgs().StartAsync(default!, default!, default);
    }

    [TestMethod]
    public async Task Shell_OpensTerminalSessionAndExecsIntoContainer()
    {
        var id = await CreateConnectionAsync();

        using var response = await SendAsync(HttpMethod.Post, $"/api/connections/{id}/docker/containers/web/shell", new { columns = 100, rows = 30, shell = "auto" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await ReadJsonAsync(response);
        body.GetProperty("connectionId").GetGuid().Should().Be(id);
        var session = _terminalFactory.Sessions.Should().ContainSingle().Subject;
        session.Input.Should().Contain("exec docker exec -it 'web' sh -c 'if command -v bash");
        using var listed = await SendAsync(HttpMethod.Get, "/api/terminals", null);
        var terminals = await ReadJsonAsync(listed);
        terminals.GetArrayLength().Should().Be(1);
    }

    [TestMethod]
    public async Task Shell_WithUnknownShell_IsBadRequest()
    {
        var id = await CreateConnectionAsync();

        using var response = await SendAsync(HttpMethod.Post, $"/api/connections/{id}/docker/containers/web/shell", new { columns = 100, rows = 30, shell = "zsh;id" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _terminalFactory.Sessions.Should().BeEmpty();
    }

    private void ReplaceServices(IServiceCollection services)
    {
        services.RemoveAll<IRemoteCommandRunnerFactory>();
        services.AddSingleton<IRemoteCommandRunnerFactory>(_runner);
        services.RemoveAll<IRemoteCommandStreamFactory>();
        services.AddSingleton(_streamFactory);
        services.RemoveAll<ITerminalSessionFactory>();
        services.AddSingleton<ITerminalSessionFactory>(_terminalFactory);
    }

    private async Task<Guid> CreateConnectionAsync()
    {
        var request = new Dictionary<string, object?>
        {
            ["name"] = "web",
            ["host"] = "web.example.test",
            ["port"] = 22,
            ["userName"] = "deploy",
            ["authMethod"] = "Agent",
        };
        using var response = await SendAsync(HttpMethod.Post, "/api/connections", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await ReadJsonAsync(response);
        return body.GetProperty("id").GetGuid();
    }

    private async Task<string> RequestConfirmationAsync(string operation, Guid connectionId, string target)
    {
        var request = new { operation, connectionId, target };
        using var response = await SendAsync(HttpMethod.Post, "/api/confirmations", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(response);
        return body.GetProperty("token").GetString()!;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("Origin", TermForgeApplicationFactory.ApplicationOrigin);
        request.Headers.Add("Cookie", _session.CookieHeader);
        request.Headers.Add("X-CSRF-Token", _session.CsrfToken);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await _client.SendAsync(request, TestContext.CancellationToken);
    }

    private async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.CancellationToken);
    }

    private async Task<TerminalSocket> ConnectLogsAsync(string path)
    {
        var client = _factory.Server.CreateWebSocketClient();
        client.ConfigureRequest = ApplyHeaders;
        var uri = new Uri($"ws://localhost{path}");
        var webSocket = await client.ConnectAsync(uri, TestContext.CancellationToken);
        return new TerminalSocket(webSocket);
    }

    private void ApplyHeaders(HttpRequest request)
    {
        request.Headers.Origin = TermForgeApplicationFactory.ApplicationOrigin;
        request.Headers.Cookie = _session.CookieHeader;
    }
}
