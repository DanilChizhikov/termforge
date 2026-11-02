using System.Net.WebSockets;
using DTech.Entrypoint.Realtime;
using DTech.Entrypoint.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DTech.Entrypoint.Tests.Realtime;

[TestClass]
public sealed class RealtimeShutdownTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Application_stopping_aborts_realtime_receive_and_removes_browser_presence()
    {
        await using var factory = new TermForgeApplicationFactory();
        using var client = factory.CreateClientWithoutCookies();
        var session = await SessionBootstrapper.AuthenticateAsync(factory, client, TestContext.CancellationToken);
        var sockets = factory.Server.CreateWebSocketClient();
        sockets.SubProtocols.Add($"termforge.{session.CsrfToken}");
        sockets.ConfigureRequest = request => ApplyHeaders(request, session);
        var uri = new Uri("ws://localhost/api/ws");
        using var socket = await sockets.ConnectAsync(uri, TestContext.CancellationToken);
        var channel = factory.Services.GetRequiredService<RealtimeChannel>();
        await WaitForConnectionAsync(channel);
        channel.ConnectionCount.Should().Be(1);
        var buffer = new byte[16];
        var receive = socket.ReceiveAsync(buffer, TestContext.CancellationToken);
        var lifetime = factory.Services.GetRequiredService<IHostApplicationLifetime>();

        lifetime.StopApplication();

        try
        {
            var timeout = TimeSpan.FromSeconds(5);
            await receive.WaitAsync(timeout, TestContext.CancellationToken);
        }
        catch (Exception exception) when (exception is WebSocketException or IOException or ObjectDisposedException)
        {
        }

        channel.ConnectionCount.Should().Be(0);
    }

    private async Task WaitForConnectionAsync(RealtimeChannel channel)
    {
        using var readiness = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var timeout = TimeSpan.FromSeconds(5);
        readiness.CancelAfter(timeout);
        while (channel.ConnectionCount == 0)
        {
            await Task.Delay(10, readiness.Token);
        }
    }

    private static void ApplyHeaders(HttpRequest request, AuthenticatedSession session)
    {
        request.Headers.Origin = TermForgeApplicationFactory.ApplicationOrigin;
        request.Headers.Cookie = session.CookieHeader;
    }
}
