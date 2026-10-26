using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using DTech.Core.Connections;
using DTech.Entrypoint.Tests.Infrastructure;
using DTech.HostKeys.Decisions;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace DTech.Entrypoint.Tests.HostKeys;

[TestClass]
public sealed class HostKeyEndpointsTests
{
    private const string PendingPath = "/api/host-keys/pending";

    private static readonly Uri ChannelUri = new("ws://localhost/api/ws");
    private static readonly SshEndpoint Endpoint = new("web-1.example", 22);
    private static readonly HostKey PresentedKey = new("ssh-ed25519", "SHA256:presentedPresentedPresentedPresentedPresen");
    private static readonly HostKey StaleKey = new("ssh-ed25519", "SHA256:stalestalestalestalestalestalestalestalesta");

    private TermForgeApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Initialize()
    {
        _factory = new TermForgeApplicationFactory();
        _client = _factory.CreateClientWithoutCookies();
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [TestMethod]
    public async Task Pending_without_session_is_unauthorized()
    {
        using var response = await TestRequest.Get(PendingPath).SendAsync(_client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task Decision_without_session_is_unauthorized()
    {
        var path = $"{PendingPath}/{Guid.NewGuid()}/decision";
        var body = new { decision = "reject" };

        using var response = await TestRequest.Post(path)
            .WithOrigin(TermForgeApplicationFactory.ApplicationOrigin)
            .WithJson(body)
            .SendAsync(_client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task Pending_lists_waiting_unknown_key_with_fingerprint()
    {
        var session = await Authenticate();
        var unknownVerification = new HostKeyVerification(HostKeyStatus.Unknown, []);
        var askTask = Ask(unknownVerification);

        var pending = await ReadPending(session);
        var item = pending[0];
        var pendingId = item.GetProperty("id").GetGuid();
        using var rejection = await Decide(session, pendingId, "reject", false);
        var outcome = await askTask;

        pending.GetArrayLength().Should().Be(1);
        item.GetProperty("host").GetString().Should().Be(Endpoint.Host);
        item.GetProperty("port").GetInt32().Should().Be(Endpoint.Port);
        item.GetProperty("status").GetString().Should().Be("unknown");
        item.GetProperty("presentedKey").GetProperty("algorithm").GetString().Should().Be(PresentedKey.Algorithm);
        item.GetProperty("presentedKey").GetProperty("fingerprint").GetString().Should().Be(PresentedKey.Sha256Fingerprint);
        outcome.Should().Be(HostKeyPromptOutcome.Rejected);
    }

    [TestMethod]
    public async Task Accept_and_save_resolves_connection_and_persists_key()
    {
        var session = await Authenticate();
        var unknownVerification = new HostKeyVerification(HostKeyStatus.Unknown, []);
        var askTask = Ask(unknownVerification);
        var pendingId = await WaitForPendingId(session);

        using var response = await Decide(session, pendingId, "acceptAndSave", false);
        var outcome = await askTask;

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        outcome.Should().Be(HostKeyPromptOutcome.Accepted);
        var trustedStore = _factory.Services.GetRequiredService<ITrustedHostKeyStore>();
        trustedStore.GetTrustedHostKeys(Endpoint).Should().Equal(PresentedKey);
    }

    [TestMethod]
    public async Task Changed_key_cannot_be_accepted_and_replace_requires_confirmation()
    {
        var session = await Authenticate();
        var changedVerification = new HostKeyVerification(HostKeyStatus.Changed, [StaleKey]);
        var askTask = Ask(changedVerification);
        var pendingId = await WaitForPendingId(session);

        using var acceptOnce = await Decide(session, pendingId, "acceptOnce", false);
        using var replaceWithoutConfirmation = await Decide(session, pendingId, "replace", false);
        using var confirmedReplace = await Decide(session, pendingId, "replace", true);
        var outcome = await askTask;

        acceptOnce.StatusCode.Should().Be(HttpStatusCode.Conflict);
        replaceWithoutConfirmation.StatusCode.Should().Be((HttpStatusCode)StatusCodes.Status428PreconditionRequired);
        var problem = await replaceWithoutConfirmation.Content.ReadAsStringAsync(TestContext.CancellationToken);
        problem.Should().Contain("ConfirmationRequired");
        confirmedReplace.StatusCode.Should().Be(HttpStatusCode.NoContent);
        outcome.Should().Be(HostKeyPromptOutcome.Accepted);
    }

    [TestMethod]
    public async Task Decision_for_unknown_id_is_not_found()
    {
        var session = await Authenticate();
        var unknownId = Guid.NewGuid();

        using var response = await Decide(session, unknownId, "reject", false);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task Decision_with_unsupported_value_is_bad_request()
    {
        var session = await Authenticate();
        var unknownId = Guid.NewGuid();

        using var response = await Decide(session, unknownId, "trustForever", false);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task Pending_changes_are_pushed_to_realtime_channel()
    {
        var session = await Authenticate();
        var webSocketClient = _factory.Server.CreateWebSocketClient();
        webSocketClient.ConfigureRequest = request => ApplyHeaders(request, session.CookieHeader);
        using var webSocket = await webSocketClient.ConnectAsync(ChannelUri, TestContext.CancellationToken);
        var unknownVerification = new HostKeyVerification(HostKeyStatus.Unknown, []);

        var askTask = Ask(unknownVerification);
        var addedMessage = await Receive(webSocket);
        var pendingId = addedMessage.GetProperty("data")[0].GetProperty("id").GetGuid();
        using var rejection = await Decide(session, pendingId, "reject", false);
        var removedMessage = await Receive(webSocket);
        await askTask;

        addedMessage.GetProperty("type").GetString().Should().Be("hostKeys.pendingChanged");
        addedMessage.GetProperty("data").GetArrayLength().Should().Be(1);
        removedMessage.GetProperty("type").GetString().Should().Be("hostKeys.pendingChanged");
        removedMessage.GetProperty("data").GetArrayLength().Should().Be(0);
    }

    private Task<HostKeyPromptOutcome> Ask(HostKeyVerification verification)
    {
        var prompt = _factory.Services.GetRequiredService<IHostKeyTrustPrompt>();
        var request = new HostKeyVerificationRequest(Endpoint, PresentedKey);
        var challenge = new HostKeyChallenge(request, verification);
        return prompt.AskAsync(challenge, TestContext.CancellationToken);
    }

    private async Task<AuthenticatedSession> Authenticate()
    {
        return await SessionBootstrapper.AuthenticateAsync(_factory, _client, TestContext.CancellationToken);
    }

    private async Task<Guid> WaitForPendingId(AuthenticatedSession session)
    {
        var pending = await ReadPending(session);
        return pending[0].GetProperty("id").GetGuid();
    }

    private async Task<JsonElement> ReadPending(AuthenticatedSession session)
    {
        while (true)
        {
            using var response = await TestRequest.Get(PendingPath).WithSession(session).SendAsync(_client, TestContext.CancellationToken);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var pending = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.CancellationToken);
            if (pending.GetArrayLength() > 0)
            {
                return pending;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(20), TestContext.CancellationToken);
        }
    }

    private Task<HttpResponseMessage> Decide(AuthenticatedSession session, Guid pendingId, string decision, bool confirmReplace)
    {
        var body = new { decision, confirmReplace };
        return TestRequest.Post($"{PendingPath}/{pendingId}/decision")
            .WithSession(session)
            .WithOrigin(TermForgeApplicationFactory.ApplicationOrigin)
            .WithCsrfToken(session.CsrfToken)
            .WithJson(body)
            .SendAsync(_client, TestContext.CancellationToken);
    }

    private async Task<JsonElement> Receive(WebSocket webSocket)
    {
        var buffer = new byte[8192];
        var result = await webSocket.ReceiveAsync(buffer, TestContext.CancellationToken);
        var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    private static void ApplyHeaders(HttpRequest request, string cookieHeader)
    {
        request.Headers.Origin = TermForgeApplicationFactory.ApplicationOrigin;
        request.Headers.Cookie = cookieHeader;
    }
}
