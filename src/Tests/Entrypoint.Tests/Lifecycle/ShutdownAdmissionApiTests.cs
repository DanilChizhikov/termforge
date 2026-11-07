using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text.Json;
using DTech.Entrypoint.Lifecycle;
using DTech.Entrypoint.Realtime;
using DTech.Entrypoint.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace DTech.Entrypoint.Tests.Lifecycle;

[TestClass]
public sealed class ShutdownAdmissionApiTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task WaitingForExit_RejectsAuthenticatedWebSocketsAndReopensAfterCancellation()
    {
        await using var factory = new TermForgeApplicationFactory();
        using var client = factory.CreateClientWithoutCookies();
        var session = await SessionBootstrapper.AuthenticateAsync(factory, client, TestContext.CancellationToken);
        var sockets = factory.Server.CreateWebSocketClient();
        sockets.SubProtocols.Add($"termforge.{session.CsrfToken}");
        sockets.ConfigureRequest = request => ApplyHeaders(request, session);
        var uri = new Uri("ws://localhost/api/ws");
        var gate = factory.Services.GetRequiredService<WorkAdmissionGate>();
        gate.TryBeginWaiting().Should().BeTrue();
        var connect = () => sockets.ConnectAsync(uri, TestContext.CancellationToken);

        await connect.Should().ThrowAsync<InvalidOperationException>().WithMessage("*503*");

        var presence = factory.Services.GetRequiredService<RealtimeChannel>();
        presence.ConnectionCount.Should().Be(0);
        gate.TryReopen().Should().BeTrue();
        using var socket = await sockets.ConnectAsync(uri, TestContext.CancellationToken);
        await WaitForConnectionAsync(presence);
        presence.ConnectionCount.Should().Be(1);
        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "closed", TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task Exiting_RejectsAuthenticatedMcpBeforeProtocolDispatch()
    {
        await using var factory = new TermForgeApplicationFactory();
        using var client = factory.CreateClientWithoutCookies();
        var session = await SessionBootstrapper.AuthenticateAsync(factory, client, TestContext.CancellationToken);
        var tokenRequest = new { name = "Shutdown test" };
        using var tokenResponse = await TestRequest.Post("/api/ai-bridge/tokens").WithJson(tokenRequest)
            .WithOrigin(TermForgeApplicationFactory.ApplicationOrigin)
            .WithSession(session).WithCsrfToken(session.CsrfToken)
            .SendAsync(client, TestContext.CancellationToken);
        tokenResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var tokenBody = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.CancellationToken);
        var token = tokenBody.GetProperty("token").GetString();
        using var settingsRequest = new HttpRequestMessage(HttpMethod.Put, "/api/ai-bridge/settings");
        settingsRequest.Headers.Add("Origin", TermForgeApplicationFactory.ApplicationOrigin);
        settingsRequest.Headers.Add("Cookie", session.CookieHeader);
        settingsRequest.Headers.Add("X-CSRF-Token", session.CsrfToken);
        var settings = new { enabled = true, requireApproval = true };
        settingsRequest.Content = JsonContent.Create(settings);
        using var settingsResponse = await client.SendAsync(settingsRequest, TestContext.CancellationToken);
        settingsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var gate = factory.Services.GetRequiredService<WorkAdmissionGate>();
        gate.Close();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var message = new { jsonrpc = "2.0", id = 1, method = "tools/list" };
        request.Content = JsonContent.Create(message);

        using var response = await client.SendAsync(request, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.CancellationToken);
        problem.GetProperty("code").GetString().Should().Be(WorkAdmissionMiddleware.ExitingCode);
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
