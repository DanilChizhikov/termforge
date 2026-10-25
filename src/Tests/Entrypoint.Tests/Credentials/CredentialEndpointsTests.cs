using System.Net;
using System.Text.Json;
using DTech.Core.Credentials;
using DTech.Entrypoint.Credentials;
using DTech.Entrypoint.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace DTech.Entrypoint.Tests.Credentials;

[TestClass]
public sealed class CredentialEndpointsTests
{
    private const string MasterPassword = "correct horse battery staple";
    private const string UnlockPath = "/api/credentials/vault/unlock";
    private const string LockPath = "/api/credentials/vault/lock";
    private const string StatusPath = "/api/credentials/status";

    private ICredentialVault _vault = null!;
    private ICredentialStoreStatusProvider _statusProvider = null!;
    private TermForgeApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Initialize()
    {
        _vault = Substitute.For<ICredentialVault>();
        _statusProvider = Substitute.For<ICredentialStoreStatusProvider>();
        _factory = new TermForgeApplicationFactory
        {
            ConfigureServices = ReplaceCredentialServices,
        };
        _client = _factory.CreateClientWithoutCookies();
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [TestMethod]
    public async Task Unlock_WithCorrectPassword_ReturnsNoContentAndPassesPasswordAsSecret()
    {
        string? receivedPassword = null;
        var success = CredentialResult.Success();
        _vault.Unlock(default!, default).ReturnsForAnyArgs(success).AndDoes(call => receivedPassword = call.Arg<Secret>().RevealString());
        var session = await Authenticate();

        using var response = await SendUnlock(session, MasterPassword);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        receivedPassword.Should().Be(MasterPassword);
    }

    [TestMethod]
    public async Task Unlock_WithWrongPassword_ReturnsProblemWithoutEchoingPassword()
    {
        var error = CredentialErrors.InvalidMasterPassword();
        var failure = CredentialResult.Failure(error);
        _vault.Unlock(default!, default).ReturnsForAnyArgs(failure);
        var session = await Authenticate();

        using var response = await SendUnlock(session, MasterPassword);
        var body = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().Contain(nameof(CredentialErrorCode.InvalidMasterPassword));
        body.Should().NotContain(MasterPassword);
    }

    [TestMethod]
    public async Task Unlock_WithoutPassword_ReturnsBadRequestAndDoesNotCallVault()
    {
        var session = await Authenticate();

        using var response = await SendUnlock(session, null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _ = _vault.DidNotReceiveWithAnyArgs().Unlock(default!, default);
    }

    [TestMethod]
    public async Task Unlock_WhenVaultIsNotInUse_ReturnsConflict()
    {
        var error = CredentialErrors.VaultNotInUse();
        var failure = CredentialResult.Failure(error);
        _vault.Unlock(default!, default).ReturnsForAnyArgs(failure);
        var session = await Authenticate();

        using var response = await SendUnlock(session, MasterPassword);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [TestMethod]
    public async Task Unlock_WithoutSession_ReturnsUnauthorizedAndDoesNotCallVault()
    {
        var request = new UnlockVaultRequest { MasterPassword = MasterPassword };

        using var response = await TestRequest.Post(UnlockPath)
            .WithOrigin(TermForgeApplicationFactory.ApplicationOrigin)
            .WithCsrfToken("guessed-token")
            .WithJson(request)
            .SendAsync(_client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _ = _vault.DidNotReceiveWithAnyArgs().Unlock(default!, default);
    }

    [TestMethod]
    public async Task Unlock_WithoutCsrfToken_ReturnsForbiddenAndDoesNotCallVault()
    {
        var session = await Authenticate();
        var request = new UnlockVaultRequest { MasterPassword = MasterPassword };

        using var response = await TestRequest.Post(UnlockPath)
            .WithOrigin(TermForgeApplicationFactory.ApplicationOrigin)
            .WithSession(session)
            .WithJson(request)
            .SendAsync(_client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _ = _vault.DidNotReceiveWithAnyArgs().Unlock(default!, default);
    }

    [TestMethod]
    public async Task Lock_WithSessionAndCsrfToken_ReturnsNoContentAndLocksVault()
    {
        var session = await Authenticate();

        using var response = await TestRequest.Post(LockPath)
            .WithOrigin(TermForgeApplicationFactory.ApplicationOrigin)
            .WithSession(session)
            .WithCsrfToken(session.CsrfToken)
            .SendAsync(_client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        _vault.Received(1).Lock();
    }

    [TestMethod]
    public async Task Lock_WithoutSession_ReturnsUnauthorizedAndKeepsVaultUnlocked()
    {
        using var response = await TestRequest.Post(LockPath)
            .WithOrigin(TermForgeApplicationFactory.ApplicationOrigin)
            .WithCsrfToken("guessed-token")
            .SendAsync(_client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _vault.DidNotReceive().Lock();
    }

    [TestMethod]
    public async Task Lock_WithoutCsrfToken_ReturnsForbiddenAndKeepsVaultUnlocked()
    {
        var session = await Authenticate();

        using var response = await TestRequest.Post(LockPath)
            .WithOrigin(TermForgeApplicationFactory.ApplicationOrigin)
            .WithSession(session)
            .SendAsync(_client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _vault.DidNotReceive().Lock();
    }

    [TestMethod]
    public async Task Status_WithSession_ReturnsBackendDiagnosticAndVaultState()
    {
        var availability = CredentialStoreAvailability.Unavailable("Secret Service (libsecret)", "No D-Bus session bus was found.");
        var status = new CredentialStoreStatus(CredentialBackend.EncryptedVault, "fallback reason", availability, CredentialVaultState.Locked);
        _statusProvider.GetStatus(default).ReturnsForAnyArgs(status);
        var session = await Authenticate();

        using var response = await TestRequest.Get(StatusPath)
            .WithSession(session)
            .SendAsync(_client, TestContext.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var operatingSystemStore = root.GetProperty("operatingSystemStore");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        root.GetProperty("backend").GetString().Should().Be("EncryptedVault");
        root.GetProperty("vaultState").GetString().Should().Be("Locked");
        root.GetProperty("selectionReason").GetString().Should().Be("fallback reason");
        operatingSystemStore.GetProperty("isAvailable").GetBoolean().Should().BeFalse();
        operatingSystemStore.GetProperty("diagnostic").GetString().Should().Be("No D-Bus session bus was found.");
    }

    [TestMethod]
    public async Task Status_WithoutSession_ReturnsUnauthorized()
    {
        using var response = await TestRequest.Get(StatusPath).SendAsync(_client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _ = _statusProvider.DidNotReceiveWithAnyArgs().GetStatus(default);
    }

    [TestMethod]
    public void UnlockVaultRequest_ToString_IsRedacted()
    {
        var request = new UnlockVaultRequest { MasterPassword = MasterPassword };

        var text = request.ToString();

        text.Should().NotContain(MasterPassword).And.Contain(Secret.Redacted);
    }

    private void ReplaceCredentialServices(IServiceCollection services)
    {
        services.RemoveAll<ICredentialVault>();
        services.RemoveAll<ICredentialStoreStatusProvider>();
        services.AddSingleton(_vault);
        services.AddSingleton(_statusProvider);
    }

    private Task<AuthenticatedSession> Authenticate()
    {
        return SessionBootstrapper.AuthenticateAsync(_factory, _client, TestContext.CancellationToken);
    }

    private Task<HttpResponseMessage> SendUnlock(AuthenticatedSession session, string? masterPassword)
    {
        var request = new UnlockVaultRequest { MasterPassword = masterPassword };
        return TestRequest.Post(UnlockPath)
            .WithOrigin(TermForgeApplicationFactory.ApplicationOrigin)
            .WithSession(session)
            .WithCsrfToken(session.CsrfToken)
            .WithJson(request)
            .SendAsync(_client, TestContext.CancellationToken);
    }
}
