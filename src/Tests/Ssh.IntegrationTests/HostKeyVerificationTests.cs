using DTech.Core.Connections;
using DTech.Core.Files;
using DTech.Core.Terminal;
using DTech.Ssh.IntegrationTests.Harness;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace DTech.Ssh.IntegrationTests;

[TestClass]
public sealed class HostKeyVerificationTests
{
    private static readonly TerminalSize InitialSize = new(80, 24);

    [TestMethod]
    public async Task OpenTerminal_WithUnknownHostKey_IsRejectedByVerifier()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        var verifier = SshTransportTestServices.CreateRejectingVerifier();
        await using var services = SshTransportTestServices.Create(verifier);
        var factory = services.GetRequiredService<ITerminalSessionFactory>();
        var target = cluster.Target.CreateKeyTarget();
        var request = new TerminalSessionRequest(target, InitialSize, TerminalSessionRequest.DefaultTerminalType);

        var result = await factory.OpenAsync(request, CancellationToken.None);

        var expectedHostKey = cluster.Target.Identity.HostKey;
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(SshErrorKind.HostKeyRejected);
        result.Error.PresentedHostKey.Should().Be(expectedHostKey);
        verifier.Received(1).Verify(Arg.Is<HostKeyVerificationRequest>(sent => sent.Endpoint == target.Endpoint && sent.HostKey == expectedHostKey));
    }

    [TestMethod]
    public async Task ConnectSftp_WithUnknownHostKey_IsRejectedByVerifier()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        var verifier = SshTransportTestServices.CreateRejectingVerifier();
        await using var services = SshTransportTestServices.Create(verifier);
        var factory = services.GetRequiredService<IRemoteFileClientFactory>();
        var target = cluster.Target.CreateKeyTarget();

        var result = await factory.ConnectAsync(target, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(SshErrorKind.HostKeyRejected);
        result.Error.PresentedHostKey.Should().Be(cluster.Target.Identity.HostKey);
    }

    [TestMethod]
    public async Task Connect_WhenPinnedKeyDiffersFromPresentedKey_IsRejected()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        var verifier = SshTransportTestServices.CreatePinnedVerifier(cluster.Jump.Identity.HostKey);
        await using var services = SshTransportTestServices.Create(verifier);
        var factory = services.GetRequiredService<IRemoteFileClientFactory>();
        var target = cluster.Target.CreateKeyTarget();

        var result = await factory.ConnectAsync(target, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(SshErrorKind.HostKeyRejected);
        result.Error.Message.Should().Contain(cluster.Target.Identity.HostKey.Sha256Fingerprint);
    }

    [TestMethod]
    public async Task Connect_WhenVerifierThrows_IsRejectedWithReason()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        var verifier = Substitute.For<IHostKeyVerifier>();
        var verifierFailure = new InvalidOperationException("known hosts store is unavailable");
        verifier.Verify(Arg.Any<HostKeyVerificationRequest>()).Returns(_ => throw verifierFailure);
        await using var services = SshTransportTestServices.Create(verifier);
        var factory = services.GetRequiredService<IRemoteFileClientFactory>();
        var target = cluster.Target.CreateKeyTarget();

        var result = await factory.ConnectAsync(target, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(SshErrorKind.HostKeyRejected);
        result.Error.Message.Should().Contain("known hosts store is unavailable");
    }

    [TestMethod]
    public async Task Connect_WithPinnedHostKey_ReportsOpenSshCompatibleFingerprintAndConnects()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        var expectedHostKey = cluster.Target.Identity.HostKey;
        var verifier = SshTransportTestServices.CreatePinnedVerifier(expectedHostKey);
        await using var services = SshTransportTestServices.Create(verifier);
        var factory = services.GetRequiredService<IRemoteFileClientFactory>();
        var target = cluster.Target.CreateKeyTarget();

        var result = await factory.ConnectAsync(target, CancellationToken.None);
        await using var client = result.IsSuccess ? result.Value : null;

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        expectedHostKey.Algorithm.Should().Be("ssh-ed25519");
        expectedHostKey.Sha256Fingerprint.Should().StartWith("SHA256:");
        verifier.Received(1).Verify(Arg.Is<HostKeyVerificationRequest>(sent => sent.HostKey == expectedHostKey));
    }
}
