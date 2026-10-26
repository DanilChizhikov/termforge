using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DTech.Core.Terminal;
using DTech.Core.Terminal.Sessions;
using DTech.Entrypoint.Tests.Infrastructure;
using DTech.Entrypoint.Tests.Terminals.Support;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace DTech.Entrypoint.Tests.Terminals;

[TestClass]
public sealed class TerminalEndpointsTests
{
    private const string TerminalsPath = "/api/terminals";
    private const string ConnectionsPath = "/api/connections";

    private ScriptedTerminalSessionFactory _sessionFactory = null!;
    private TermForgeApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private AuthenticatedSession _session = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public async Task Initialize()
    {
        _sessionFactory = new ScriptedTerminalSessionFactory();
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
    public async Task Open_ReturnsUnguessableSessionWithServerIdentification()
    {
        var connectionId = await CreateConnectionAsync("web");

        var opened = await OpenTerminalAsync(connectionId, 100, 30);
        var second = await OpenTerminalAsync(connectionId, 100, 30);
        using var listed = await Send(HttpMethod.Get, TerminalsPath, null);
        var list = await listed.Content.ReadFromJsonAsync<JsonElement>(TestContext.CancellationToken);

        opened.GetProperty("id").GetString().Should().MatchRegex("^[0-9a-f]{64}$");
        opened.GetProperty("connectionId").GetGuid().Should().Be(connectionId);
        opened.GetProperty("connectionName").GetString().Should().Be("web");
        opened.GetProperty("sessionNumber").GetInt32().Should().Be(1);
        second.GetProperty("sessionNumber").GetInt32().Should().Be(2);
        list.GetArrayLength().Should().Be(2);
        _sessionFactory.Requests[0].InitialSize.Should().Be(new TerminalSize(100, 30));
        _sessionFactory.Requests[0].Target.Endpoint.Host.Should().Be("web.example.test");
    }

    [TestMethod]
    public async Task Open_ForUnknownConnection_ReturnsNotFound()
    {
        using var response = await Send(HttpMethod.Post, TerminalsPath, new { connectionId = Guid.NewGuid(), columns = 80, rows = 24 });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        _sessionFactory.Sessions.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Open_WithoutCsrfToken_IsRejected()
    {
        var connectionId = await CreateConnectionAsync("web");

        using var response = await TestRequest.Post(TerminalsPath)
            .WithOrigin(TermForgeApplicationFactory.ApplicationOrigin)
            .WithSession(_session)
            .WithJson(new { connectionId, columns = 80, rows = 24 })
            .SendAsync(_client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _sessionFactory.Sessions.Should().BeEmpty();
    }

    [TestMethod]
    public async Task WebSocket_RelaysInputOutputAndResize()
    {
        var connectionId = await CreateConnectionAsync("web");
        var opened = await OpenTerminalAsync(connectionId, 80, 24);
        using var socket = await AttachAsync(opened.GetProperty("id").GetString()!);
        var connected = await socket.ExpectMessageAsync("status");
        var remote = _sessionFactory.Sessions.Single();

        remote.Emit("welcome$ ");
        await socket.ExpectOutputAsync("welcome$ ");
        await socket.SendInputAsync("uname -a\r");
        await socket.ExpectOutputAsync("ECHO[uname -a\r]");
        await socket.SendControlAsync("""{"type":"resize","columns":132,"rows":40}""");
        await socket.ExpectOutputAsync("SIZE[40 132]");

        connected.GetProperty("state").GetString().Should().Be("connected");
        remote.Input.Should().Be("uname -a\r");
        remote.Resizes.Should().Equal(new TerminalSize(132, 40));
    }

    [TestMethod]
    public async Task WebSocket_ReportsInvalidControlMessagesWithoutClosing()
    {
        var connectionId = await CreateConnectionAsync("web");
        var opened = await OpenTerminalAsync(connectionId, 80, 24);
        using var socket = await AttachAsync(opened.GetProperty("id").GetString()!);

        await socket.SendControlAsync("""{"type":"resize","columns":0,"rows":40}""");
        var invalidSize = await socket.ExpectMessageAsync("error");
        await socket.SendControlAsync("not json");
        var garbage = await socket.ExpectMessageAsync("error");
        await socket.SendInputAsync("still alive");
        await socket.ExpectOutputAsync("ECHO[still alive]");

        invalidSize.GetProperty("message").GetString().Should().Contain("invalid");
        garbage.GetProperty("message").GetString().Should().Contain("Unsupported");
    }

    [TestMethod]
    public async Task ConcurrentSessions_AreIsolated()
    {
        var webId = await CreateConnectionAsync("web");
        var dbId = await CreateConnectionAsync("db");
        var web = await OpenTerminalAsync(webId, 80, 24);
        var db = await OpenTerminalAsync(dbId, 80, 24);
        using var webSocket = await AttachAsync(web.GetProperty("id").GetString()!);
        using var dbSocket = await AttachAsync(db.GetProperty("id").GetString()!);

        await webSocket.SendInputAsync("only-web");
        await webSocket.ExpectOutputAsync("ECHO[only-web]");
        await dbSocket.SendControlAsync("""{"type":"resize","columns":90,"rows":20}""");
        await dbSocket.ExpectOutputAsync("SIZE[20 90]");

        var webRemote = _sessionFactory.Sessions[0];
        var dbRemote = _sessionFactory.Sessions[1];
        webRemote.Input.Should().Be("only-web");
        webRemote.Resizes.Should().BeEmpty();
        dbRemote.Input.Should().BeEmpty();
        dbSocket.Output.Should().NotContain("only-web");
        webSocket.Output.Should().NotContain("SIZE");
    }

    [TestMethod]
    public async Task Delete_ClosesSessionAndTellsTheBrowserWhy()
    {
        var connectionId = await CreateConnectionAsync("web");
        var opened = await OpenTerminalAsync(connectionId, 80, 24);
        var id = opened.GetProperty("id").GetString()!;
        using var socket = await AttachAsync(id);
        await socket.ExpectMessageAsync("status");

        using var deleted = await Send(HttpMethod.Delete, $"{TerminalsPath}/{id}", null);
        var disconnected = await socket.ExpectMessageAsync("status");
        await socket.ExpectCloseAsync();
        using var deletedAgain = await Send(HttpMethod.Delete, $"{TerminalsPath}/{id}", null);

        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        disconnected.GetProperty("state").GetString().Should().Be("disconnected");
        disconnected.GetProperty("reason").GetString().Should().Be(TerminalCloseReasons.ClosedByUser);
        _sessionFactory.Sessions.Single().IsDisposed.Should().BeTrue();
        deletedAgain.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task BrowserClosingTheSocket_ReleasesTheSession()
    {
        var connectionId = await CreateConnectionAsync("web");
        var opened = await OpenTerminalAsync(connectionId, 80, 24);
        using var socket = await AttachAsync(opened.GetProperty("id").GetString()!);
        await socket.ExpectMessageAsync("status");

        await socket.WebSocket.CloseAsync(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "tab closed", TestContext.CancellationToken);
        await WaitForAsync(_sessionFactory.Sessions.Single().Disposed);

        var manager = _factory.Services.GetRequiredService<TerminalSessionManager>();
        manager.Count.Should().Be(0);
    }

    [TestMethod]
    public async Task RemoteShellExit_IsReportedAsDisconnected()
    {
        var connectionId = await CreateConnectionAsync("web");
        var opened = await OpenTerminalAsync(connectionId, 80, 24);
        using var socket = await AttachAsync(opened.GetProperty("id").GetString()!);
        await socket.ExpectMessageAsync("status");

        _sessionFactory.Sessions.Single().EndRemotely();
        var disconnected = await socket.ExpectMessageAsync("status");

        disconnected.GetProperty("state").GetString().Should().Be("disconnected");
        disconnected.GetProperty("reason").GetString().Should().Be(TerminalCloseReasons.RemoteShellExited);
        await WaitForAsync(_sessionFactory.Sessions.Single().Disposed);
    }

    [TestMethod]
    public async Task Shutdown_ClosesAttachedAndDetachedSessions()
    {
        var connectionId = await CreateConnectionAsync("web");
        var attached = await OpenTerminalAsync(connectionId, 80, 24);
        await OpenTerminalAsync(connectionId, 80, 24);
        using var socket = await AttachAsync(attached.GetProperty("id").GetString()!);
        await socket.ExpectMessageAsync("status");

        var lifetime = _factory.Services.GetRequiredService<IHostApplicationLifetime>();
        lifetime.StopApplication();
        var disconnected = await socket.ExpectMessageAsync("status");

        disconnected.GetProperty("reason").GetString().Should().Be(TerminalCloseReasons.ShuttingDown);
        _sessionFactory.Sessions.Should().HaveCount(2).And.OnlyContain(session => session.IsDisposed);
    }

    [TestMethod]
    public async Task WebSocket_WithoutBrowserSession_IsRejected()
    {
        var connectionId = await CreateConnectionAsync("web");
        var opened = await OpenTerminalAsync(connectionId, 80, 24);
        var client = CreateWebSocketClient(TermForgeApplicationFactory.ApplicationOrigin, null);
        var uri = CreateSocketUri(opened.GetProperty("id").GetString()!);

        var connect = () => client.ConnectAsync(uri, TestContext.CancellationToken);

        await connect.Should().ThrowAsync<InvalidOperationException>().WithMessage("*401*");
        _sessionFactory.Sessions.Single().IsDisposed.Should().BeFalse();
    }

    [TestMethod]
    public async Task WebSocket_FromForeignOrigin_IsRejected()
    {
        var connectionId = await CreateConnectionAsync("web");
        var opened = await OpenTerminalAsync(connectionId, 80, 24);
        var client = CreateWebSocketClient("http://attacker.example", _session.CookieHeader);
        var uri = CreateSocketUri(opened.GetProperty("id").GetString()!);

        var connect = () => client.ConnectAsync(uri, TestContext.CancellationToken);

        await connect.Should().ThrowAsync<InvalidOperationException>().WithMessage("*403*");
    }

    [TestMethod]
    public async Task WebSocket_ForUnknownSession_IsRejected()
    {
        var client = CreateWebSocketClient(TermForgeApplicationFactory.ApplicationOrigin, _session.CookieHeader);
        var unknownId = TerminalSessionId.Create();
        var uri = CreateSocketUri(unknownId.Value);

        var connect = () => client.ConnectAsync(uri, TestContext.CancellationToken);

        await connect.Should().ThrowAsync<InvalidOperationException>().WithMessage("*404*");
    }

    [TestMethod]
    public async Task WebSocket_SecondAttachment_IsRejected()
    {
        var connectionId = await CreateConnectionAsync("web");
        var opened = await OpenTerminalAsync(connectionId, 80, 24);
        var id = opened.GetProperty("id").GetString()!;
        using var first = await AttachAsync(id);
        var client = CreateWebSocketClient(TermForgeApplicationFactory.ApplicationOrigin, _session.CookieHeader);
        var uri = CreateSocketUri(id);

        var connect = () => client.ConnectAsync(uri, TestContext.CancellationToken);

        await connect.Should().ThrowAsync<InvalidOperationException>().WithMessage("*409*");
    }

    private void ReplaceServices(IServiceCollection services)
    {
        services.RemoveAll<ITerminalSessionFactory>();
        services.AddSingleton<ITerminalSessionFactory>(_sessionFactory);
    }

    private async Task<Guid> CreateConnectionAsync(string name)
    {
        var request = new { name, host = $"{name}.example.test", port = 22, userName = "deploy", authMethod = "Agent" };
        using var response = await Send(HttpMethod.Post, ConnectionsPath, request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.CancellationToken);
        return body.GetProperty("id").GetGuid();
    }

    private async Task<JsonElement> OpenTerminalAsync(Guid connectionId, int columns, int rows)
    {
        using var response = await Send(HttpMethod.Post, TerminalsPath, new { connectionId, columns, rows });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.CancellationToken);
    }

    private async Task<TerminalSocket> AttachAsync(string id)
    {
        var client = CreateWebSocketClient(TermForgeApplicationFactory.ApplicationOrigin, _session.CookieHeader);
        var uri = CreateSocketUri(id);
        var webSocket = await client.ConnectAsync(uri, TestContext.CancellationToken);
        return new TerminalSocket(webSocket);
    }

    private async Task<HttpResponseMessage> Send(HttpMethod method, string path, object? body)
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

    private WebSocketClient CreateWebSocketClient(string origin, string? cookieHeader)
    {
        var webSocketClient = _factory.Server.CreateWebSocketClient();
        webSocketClient.ConfigureRequest = request => ApplyHeaders(request, origin, cookieHeader);
        return webSocketClient;
    }

    private static void ApplyHeaders(HttpRequest request, string origin, string? cookieHeader)
    {
        request.Headers.Origin = origin;
        if (cookieHeader is not null)
        {
            request.Headers.Cookie = cookieHeader;
        }
    }

    private static Uri CreateSocketUri(string id)
    {
        return new Uri($"ws://localhost{TerminalsPath}/{id}/ws");
    }

    private async Task WaitForAsync(Task task)
    {
        var timeout = Task.Delay(TimeSpan.FromSeconds(10), TestContext.CancellationToken);
        var completed = await Task.WhenAny(task, timeout);
        completed.Should().BeSameAs(task);
    }
}
