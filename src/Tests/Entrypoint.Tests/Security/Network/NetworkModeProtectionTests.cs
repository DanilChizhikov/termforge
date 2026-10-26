using System.Net;
using System.Net.WebSockets;
using DTech.Entrypoint.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace DTech.Entrypoint.Tests.Security.Network;

[TestClass]
public sealed class NetworkModeProtectionTests
{
    private static readonly Uri NetworkRealtimeUri = new("wss://termforge.lan/api/ws");
    private static readonly Uri NetworkProbeUri = new($"wss://termforge.lan{WebSocketProbeStartupFilter.Path}");

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("/api/session")]
    [DataRow("/api/connections")]
    [DataRow("/api/connections/7b0f6b1c-2a59-4f39-9b8f-61c0f1a6a7d1/capabilities")]
    [DataRow("/api/operations")]
    [DataRow("/api/credentials/status")]
    [DataRow("/api/host-keys/pending")]
    [DataRow("/api/updates")]
    public async Task Unauthenticated_api_request_from_network_is_rejected(string path)
    {
        await using var factory = CreateNetworkFactory();
        using var client = factory.CreateClientFor(NetworkAccessSettings.NetworkBaseAddress);

        using var response = await TestRequest.Get(path).SendAsync(client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    [DataRow("/api/lifecycle/exit")]
    [DataRow("/api/connections/7b0f6b1c-2a59-4f39-9b8f-61c0f1a6a7d1/capabilities/refresh")]
    public async Task Unauthenticated_state_change_from_network_is_rejected(string path)
    {
        await using var factory = CreateNetworkFactory();
        using var client = factory.CreateClientFor(NetworkAccessSettings.NetworkBaseAddress);

        using var response = await TestRequest.Post(path)
            .WithOrigin(NetworkAccessSettings.NetworkOrigin)
            .SendAsync(client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        factory.ApplicationStopping.IsCancellationRequested.Should().BeFalse();
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("termforge_session=forged")]
    public async Task Unauthenticated_realtime_websocket_from_network_is_rejected(string? cookieHeader)
    {
        await using var factory = CreateNetworkFactory();
        var webSocketClient = CreateWebSocketClient(factory, NetworkAccessSettings.NetworkOrigin, cookieHeader);

        var connect = () => webSocketClient.ConnectAsync(NetworkRealtimeUri, TestContext.CancellationToken);

        await connect.Should().ThrowAsync<InvalidOperationException>().WithMessage("*401*");
    }

    [TestMethod]
    public async Task Websocket_with_password_session_and_foreign_origin_is_rejected()
    {
        await using var factory = CreateNetworkFactory();
        using var client = factory.CreateClientFor(NetworkAccessSettings.NetworkBaseAddress);
        var session = await PasswordSessions.SignInAsync(client, TestContext.CancellationToken);
        var webSocketClient = CreateWebSocketClient(factory, "https://attacker.example", session.CookieHeader);

        var connect = () => webSocketClient.ConnectAsync(NetworkRealtimeUri, TestContext.CancellationToken);

        await connect.Should().ThrowAsync<InvalidOperationException>().WithMessage("*403*");
    }

    [TestMethod]
    public async Task Websocket_with_password_session_and_network_origin_is_accepted()
    {
        await using var factory = CreateNetworkFactory();
        using var client = factory.CreateClientFor(NetworkAccessSettings.NetworkBaseAddress);
        var session = await PasswordSessions.SignInAsync(client, TestContext.CancellationToken);
        var webSocketClient = CreateWebSocketClient(factory, NetworkAccessSettings.NetworkOrigin, session.CookieHeader);

        using var webSocket = await webSocketClient.ConnectAsync(NetworkProbeUri, TestContext.CancellationToken);
        var buffer = new byte[16];
        var result = await webSocket.ReceiveAsync(buffer, TestContext.CancellationToken);

        result.MessageType.Should().Be(WebSocketMessageType.Close);
    }

    [TestMethod]
    [DataRow("https://attacker.example/")]
    [DataRow("https://rebind.termforge.lan/")]
    [DataRow("https://192.168.1.10/")]
    public async Task Host_outside_allow_list_is_rejected_in_network_mode(string baseAddress)
    {
        await using var factory = CreateNetworkFactory();
        using var client = factory.CreateClientFor(baseAddress);

        using var response = await TestRequest.Get(NetworkAccessSettings.LoginPath).SendAsync(client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    [DataRow("https://termforge.lan/")]
    [DataRow("https://TERMFORGE.LAN:5023/")]
    [DataRow("http://127.0.0.1:5023/")]
    [DataRow("http://localhost/")]
    public async Task Allowed_and_loopback_hosts_pass_in_network_mode(string baseAddress)
    {
        await using var factory = CreateNetworkFactory();
        using var client = factory.CreateClientFor(baseAddress);

        using var response = await TestRequest.Get(NetworkAccessSettings.LoginPath).SendAsync(client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [TestMethod]
    public async Task Launch_bootstrap_still_works_on_loopback_in_network_mode()
    {
        await using var factory = CreateNetworkFactory();
        using var client = factory.CreateClientWithoutCookies();

        var session = await SessionBootstrapper.AuthenticateAsync(factory, client, TestContext.CancellationToken);

        session.CsrfToken.Should().NotBeNullOrEmpty();
    }

    [TestMethod]
    public async Task Csrf_is_still_required_for_password_sessions()
    {
        await using var factory = CreateNetworkFactory();
        using var client = factory.CreateClientFor(NetworkAccessSettings.NetworkBaseAddress);
        var session = await PasswordSessions.SignInAsync(client, TestContext.CancellationToken);

        using var response = await TestRequest.Post("/api/lifecycle/exit")
            .WithOrigin(NetworkAccessSettings.NetworkOrigin)
            .WithSession(session)
            .SendAsync(client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        factory.ApplicationStopping.IsCancellationRequested.Should().BeFalse();
    }

    private static TermForgeApplicationFactory CreateNetworkFactory()
    {
        var settings = NetworkAccessSettings.CreateEnabled();
        return new TermForgeApplicationFactory(settings);
    }

    private static WebSocketClient CreateWebSocketClient(TermForgeApplicationFactory factory, string? origin, string? cookieHeader)
    {
        var webSocketClient = factory.Server.CreateWebSocketClient();
        webSocketClient.ConfigureRequest = request => ApplyHeaders(request, origin, cookieHeader);
        return webSocketClient;
    }

    private static void ApplyHeaders(HttpRequest request, string? origin, string? cookieHeader)
    {
        if (origin is not null)
        {
            request.Headers.Origin = origin;
        }

        if (cookieHeader is not null)
        {
            request.Headers.Cookie = cookieHeader;
        }
    }
}
