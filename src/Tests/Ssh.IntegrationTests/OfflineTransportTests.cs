using System.Net;
using System.Net.Sockets;
using DTech.Core.Connections;
using DTech.Core.Files;
using DTech.Core.Terminal;
using DTech.Ssh.IntegrationTests.Harness;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace DTech.Ssh.IntegrationTests;

[TestClass]
public sealed class OfflineTransportTests
{
    private const string UserName = "tester";

    [TestMethod]
    public async Task Connect_WithMissingPrivateKeyFile_ReturnsPrivateKeyUnusable()
    {
        var verifier = Substitute.For<IHostKeyVerifier>();
        await using var services = SshTransportTestServices.Create(verifier);
        var factory = services.GetRequiredService<IRemoteFileClientFactory>();
        var missingKeyPath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}");
        var authentication = new PrivateKeyAuthentication(missingKeyPath, null);
        var endpoint = new SshEndpoint("127.0.0.1", SshEndpoint.DefaultPort);
        var target = new SshConnectionTarget(endpoint, UserName, authentication);

        var result = await factory.ConnectAsync(target, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(SshErrorKind.PrivateKeyUnusable);
        verifier.DidNotReceiveWithAnyArgs().Verify(default!);
    }

    [TestMethod]
    public async Task Connect_WithMalformedPrivateKeyFile_ReturnsPrivateKeyUnusable()
    {
        var keyDirectory = TestKeyFiles.CreateDirectory();
        try
        {
            var keyPath = Path.Combine(keyDirectory, "malformed_key");
            var malformedContent = "-----BEGIN OPENSSH PRIVATE KEY-----\nnot a key\n-----END OPENSSH PRIVATE KEY-----\n"u8.ToArray();
            await TestKeyFiles.WritePrivateKeyAsync(keyPath, malformedContent, CancellationToken.None);
            var verifier = Substitute.For<IHostKeyVerifier>();
            await using var services = SshTransportTestServices.Create(verifier);
            var factory = services.GetRequiredService<IRemoteFileClientFactory>();
            var authentication = new PrivateKeyAuthentication(keyPath, null);
            var endpoint = new SshEndpoint("127.0.0.1", SshEndpoint.DefaultPort);
            var target = new SshConnectionTarget(endpoint, UserName, authentication);

            var result = await factory.ConnectAsync(target, CancellationToken.None);

            result.IsSuccess.Should().BeFalse();
            result.Error!.Kind.Should().Be(SshErrorKind.PrivateKeyUnusable);
        }
        finally
        {
            TestKeyFiles.DeleteDirectory(keyDirectory);
        }
    }

    [TestMethod]
    public async Task Connect_ToClosedPort_ReturnsConnectionFailed()
    {
        var verifier = Substitute.For<IHostKeyVerifier>();
        await using var services = SshTransportTestServices.Create(verifier);
        var factory = services.GetRequiredService<IRemoteFileClientFactory>();
        var closedPort = ReserveClosedPort();
        var endpoint = new SshEndpoint("127.0.0.1", closedPort);
        var authentication = new PasswordAuthentication("unused");
        var target = new SshConnectionTarget(endpoint, UserName, authentication);

        var result = await factory.ConnectAsync(target, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(SshErrorKind.ConnectionFailed);
        result.Error.Message.Should().Contain(endpoint.ToString());
    }

    [TestMethod]
    public async Task Connect_WithUnreachableAgent_ReturnsAgentUnavailable()
    {
        var verifier = Substitute.For<IHostKeyVerifier>();
        await using var services = SshTransportTestServices.Create(verifier);
        var factory = services.GetRequiredService<IRemoteFileClientFactory>();
        var missingAgentAddress = CreateMissingAgentAddress();
        var authentication = new AgentAuthentication(missingAgentAddress);
        var endpoint = new SshEndpoint("127.0.0.1", SshEndpoint.DefaultPort);
        var target = new SshConnectionTarget(endpoint, UserName, authentication);

        var result = await factory.ConnectAsync(target, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(SshErrorKind.AgentUnavailable);
    }

    [TestMethod]
    public async Task OpenTerminal_WithInvalidInitialSize_FailsBeforeConnecting()
    {
        var verifier = Substitute.For<IHostKeyVerifier>();
        await using var services = SshTransportTestServices.Create(verifier);
        var factory = services.GetRequiredService<ITerminalSessionFactory>();
        var endpoint = new SshEndpoint("127.0.0.1", SshEndpoint.DefaultPort);
        var authentication = new PasswordAuthentication("unused");
        var target = new SshConnectionTarget(endpoint, UserName, authentication);
        var invalidSize = new TerminalSize(80, 0);
        var request = new TerminalSessionRequest(target, invalidSize, TerminalSessionRequest.DefaultTerminalType);

        var result = await factory.OpenAsync(request, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(SshErrorKind.OperationFailed);
    }

    [TestMethod]
    public void ToString_DoesNotExposeSecrets()
    {
        var password = new PasswordAuthentication("password-secret");
        var privateKey = new PrivateKeyAuthentication("/keys/id_ed25519", "passphrase-secret");

        var passwordText = password.ToString();
        var privateKeyText = privateKey.ToString();

        passwordText.Should().NotContain("password-secret");
        privateKeyText.Should().NotContain("passphrase-secret").And.Contain("/keys/id_ed25519");
    }

    private static int ReserveClosedPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string CreateMissingAgentAddress()
    {
        var name = $"termforge-missing-agent-{Guid.NewGuid():N}";
        if (OperatingSystem.IsWindows())
        {
            return name;
        }

        return Path.Combine(Path.GetTempPath(), $"{name[..30]}.sock");
    }
}
