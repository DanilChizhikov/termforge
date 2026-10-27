using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using DTech.Core.Operations;
using DTech.Entrypoint.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace DTech.Entrypoint.Tests.Operations;

[TestClass]
public sealed class OperationRealtimeTests
{
    private static readonly Uri ChannelUri = new("ws://localhost/api/ws");

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Channel_without_session_is_rejected()
    {
        await using var factory = new TermForgeApplicationFactory();
        var webSocketClient = CreateWebSocketClient(factory, TermForgeApplicationFactory.ApplicationOrigin, null);

        var connect = () => webSocketClient.ConnectAsync(ChannelUri, TestContext.CancellationToken);

        await connect.Should().ThrowAsync<InvalidOperationException>().WithMessage("*401*");
    }

    [TestMethod]
    public async Task Channel_with_foreign_origin_is_rejected()
    {
        await using var factory = new TermForgeApplicationFactory();
        using var client = factory.CreateClientWithoutCookies();
        var session = await SessionBootstrapper.AuthenticateAsync(factory, client, TestContext.CancellationToken);
        var webSocketClient = CreateWebSocketClient(factory, "http://attacker.example", session.CookieHeader);

        var connect = () => webSocketClient.ConnectAsync(ChannelUri, TestContext.CancellationToken);

        await connect.Should().ThrowAsync<InvalidOperationException>().WithMessage("*403*");
    }

    [TestMethod]
    public async Task Operation_changes_are_pushed_to_connected_clients()
    {
        await using var factory = new TermForgeApplicationFactory();
        using var client = factory.CreateClientWithoutCookies();
        var session = await SessionBootstrapper.AuthenticateAsync(factory, client, TestContext.CancellationToken);
        var webSocketClient = CreateWebSocketClient(factory, TermForgeApplicationFactory.ApplicationOrigin, session.CookieHeader);
        using var webSocket = await webSocketClient.ConnectAsync(ChannelUri, TestContext.CancellationToken);

        using var scope = factory.Services.CreateScope();
        var journal = scope.ServiceProvider.GetRequiredService<IOperationJournal>();
        var request = new CreateOperationRequest("transfer", "web-1", null, false);
        var created = await journal.Create(request, TestContext.CancellationToken);
        var queuedMessage = await Receive(webSocket);
        await journal.Start(created.Id, TestContext.CancellationToken);
        var runningMessage = await Receive(webSocket);

        queuedMessage.GetProperty("type").GetString().Should().Be("operation.changed");
        queuedMessage.GetProperty("data").GetProperty("status").GetString().Should().Be("queued");
        runningMessage.GetProperty("data").GetProperty("id").GetGuid().Should().Be(created.Id);
        runningMessage.GetProperty("data").GetProperty("status").GetString().Should().Be("running");
    }

    private async Task<JsonElement> Receive(WebSocket webSocket)
    {
        var buffer = new byte[8192];
        var result = await webSocket.ReceiveAsync(buffer, TestContext.CancellationToken);
        var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    private static WebSocketClient CreateWebSocketClient(TermForgeApplicationFactory factory, string origin, string? cookieHeader)
    {
        var webSocketClient = factory.Server.CreateWebSocketClient();
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
}
