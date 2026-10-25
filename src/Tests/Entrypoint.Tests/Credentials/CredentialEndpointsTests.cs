using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DTech.Core.Credentials;
using DTech.Entrypoint.Credentials;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace DTech.Entrypoint.Tests.Credentials;

[TestClass]
public sealed class CredentialEndpointsTests
{
    private const string MasterPassword = "correct horse battery staple";

    private ICredentialVault _vault = null!;
    private ICredentialStoreStatusProvider _statusProvider = null!;
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public async Task Initialize()
    {
        _vault = Substitute.For<ICredentialVault>();
        _statusProvider = Substitute.For<ICredentialStoreStatusProvider>();
        var builder = WebApplication.CreateSlimBuilder();
        builder.Configuration["Kestrel:Endpoints:Http:Url"] = "http://127.0.0.1:0";
        builder.Services.AddSingleton(_vault);
        builder.Services.AddSingleton(_statusProvider);
        _app = builder.Build();
        _app.MapCredentialEndpoints();
        await _app.StartAsync(TestContext.CancellationToken);
        var address = _app.Urls.First();
        var baseAddress = new Uri(address);
        _client = new HttpClient { BaseAddress = baseAddress };
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        _client?.Dispose();
        await _app.DisposeAsync();
    }

    [TestMethod]
    public async Task Unlock_WithCorrectPassword_ReturnsNoContentAndPassesPasswordAsSecret()
    {
        string? receivedPassword = null;
        var success = CredentialResult.Success();
        _vault.Unlock(default!, default).ReturnsForAnyArgs(success).AndDoes(call => receivedPassword = call.Arg<Secret>().RevealString());
        var request = new UnlockVaultRequest { MasterPassword = MasterPassword };

        var response = await _client.PostAsJsonAsync("/api/credentials/vault/unlock", request, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        receivedPassword.Should().Be(MasterPassword);
    }

    [TestMethod]
    public async Task Unlock_WithWrongPassword_ReturnsProblemWithoutEchoingPassword()
    {
        var error = CredentialErrors.InvalidMasterPassword();
        var failure = CredentialResult.Failure(error);
        _vault.Unlock(default!, default).ReturnsForAnyArgs(failure);
        var request = new UnlockVaultRequest { MasterPassword = MasterPassword };

        var response = await _client.PostAsJsonAsync("/api/credentials/vault/unlock", request, TestContext.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().Contain(nameof(CredentialErrorCode.InvalidMasterPassword));
        body.Should().NotContain(MasterPassword);
    }

    [TestMethod]
    public async Task Unlock_WithoutPassword_ReturnsBadRequestAndDoesNotCallVault()
    {
        var request = new UnlockVaultRequest();

        var response = await _client.PostAsJsonAsync("/api/credentials/vault/unlock", request, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _ = _vault.DidNotReceiveWithAnyArgs().Unlock(default!, default);
    }

    [TestMethod]
    public async Task Unlock_WhenVaultIsNotInUse_ReturnsConflict()
    {
        var error = CredentialErrors.VaultNotInUse();
        var failure = CredentialResult.Failure(error);
        _vault.Unlock(default!, default).ReturnsForAnyArgs(failure);
        var request = new UnlockVaultRequest { MasterPassword = MasterPassword };

        var response = await _client.PostAsJsonAsync("/api/credentials/vault/unlock", request, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [TestMethod]
    public async Task Lock_ReturnsNoContentAndLocksVault()
    {
        using var content = new StringContent(string.Empty);

        var response = await _client.PostAsync("/api/credentials/vault/lock", content, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        _vault.Received(1).Lock();
    }

    [TestMethod]
    public async Task Status_ReturnsBackendDiagnosticAndVaultState()
    {
        var availability = CredentialStoreAvailability.Unavailable("Secret Service (libsecret)", "No D-Bus session bus was found.");
        var status = new CredentialStoreStatus(CredentialBackend.EncryptedVault, "fallback reason", availability, CredentialVaultState.Locked);
        _statusProvider.GetStatus(default).ReturnsForAnyArgs(status);

        var response = await _client.GetAsync("/api/credentials/status", TestContext.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        root.GetProperty("backend").GetString().Should().Be("EncryptedVault");
        root.GetProperty("vaultState").GetString().Should().Be("Locked");
        root.GetProperty("selectionReason").GetString().Should().Be("fallback reason");
        root.GetProperty("operatingSystemStore").GetProperty("isAvailable").GetBoolean().Should().BeFalse();
        root.GetProperty("operatingSystemStore").GetProperty("diagnostic").GetString().Should().Be("No D-Bus session bus was found.");
    }

    [TestMethod]
    public void UnlockVaultRequest_ToString_IsRedacted()
    {
        var request = new UnlockVaultRequest { MasterPassword = MasterPassword };

        var text = request.ToString();

        text.Should().NotContain(MasterPassword).And.Contain(Secret.Redacted);
    }
}
