using System.Net.WebSockets;
using DTech.Entrypoint.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace DTech.Entrypoint.Tests.Security;

[TestClass]
public sealed class WebSocketSecurityTests
{
    private static readonly Uri ProbeUri = new($"ws://localhost{WebSocketProbeStartupFilter.Path}");

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("http://attacker.example")]
    [DataRow("http://localhost:8080")]
    [DataRow("null")]
    public async Task WebSocket_with_foreign_origin_is_rejected_even_with_session(string origin)
    {
        await using var factory = new TermForgeApplicationFactory();
        using var client = factory.CreateClientWithoutCookies();
        var session = await SessionBootstrapper.AuthenticateAsync(factory, client, TestContext.CancellationToken);
        var webSocketClient = CreateWebSocketClient(factory, origin, session.CookieHeader);

        var connect = () => webSocketClient.ConnectAsync(ProbeUri, TestContext.CancellationToken);

        await connect.Should().ThrowAsync<InvalidOperationException>().WithMessage("*403*");
    }

    [TestMethod]
    public async Task WebSocket_without_origin_is_rejected_even_with_session()
    {
        await using var factory = new TermForgeApplicationFactory();
        using var client = factory.CreateClientWithoutCookies();
        var session = await SessionBootstrapper.AuthenticateAsync(factory, client, TestContext.CancellationToken);
        var webSocketClient = CreateWebSocketClient(factory, null, session.CookieHeader);

        var connect = () => webSocketClient.ConnectAsync(ProbeUri, TestContext.CancellationToken);

        await connect.Should().ThrowAsync<InvalidOperationException>().WithMessage("*403*");
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("termforge_session=forged")]
    public async Task WebSocket_without_valid_session_is_rejected(string? cookieHeader)
    {
        await using var factory = new TermForgeApplicationFactory();
        var webSocketClient = CreateWebSocketClient(factory, TermForgeApplicationFactory.ApplicationOrigin, cookieHeader);

        var connect = () => webSocketClient.ConnectAsync(ProbeUri, TestContext.CancellationToken);

        await connect.Should().ThrowAsync<InvalidOperationException>().WithMessage("*401*");
    }

    [TestMethod]
    public async Task WebSocket_with_same_origin_and_session_is_accepted()
    {
        await using var factory = new TermForgeApplicationFactory();
        using var client = factory.CreateClientWithoutCookies();
        var session = await SessionBootstrapper.AuthenticateAsync(factory, client, TestContext.CancellationToken);
        var webSocketClient = CreateWebSocketClient(factory, TermForgeApplicationFactory.ApplicationOrigin, session.CookieHeader);

        using var webSocket = await webSocketClient.ConnectAsync(ProbeUri, TestContext.CancellationToken);
        var buffer = new byte[16];
        var result = await webSocket.ReceiveAsync(buffer, TestContext.CancellationToken);

        result.MessageType.Should().Be(WebSocketMessageType.Close);
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
