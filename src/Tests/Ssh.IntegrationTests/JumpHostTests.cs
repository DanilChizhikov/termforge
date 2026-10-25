using System.Text.RegularExpressions;
using DTech.Core.Connections;
using DTech.Core.Files;
using DTech.Core.Terminal;
using DTech.Ssh.IntegrationTests.Harness;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace DTech.Ssh.IntegrationTests;

[TestClass]
public sealed class JumpHostTests
{
    private static readonly TerminalSize InitialSize = new(80, 24);
    private static readonly Regex HostNamePattern = new(@"HOST\[([a-z0-9-]+)\]");

    [TestMethod]
    public async Task OpenTerminal_ThroughJumpHost_ReachesHostVisibleOnlyFromJumpHost()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        var verifier = SshTransportTestServices.CreatePinnedVerifier(cluster.Target.Identity.HostKey, cluster.Jump.Identity.HostKey);
        await using var services = SshTransportTestServices.Create(verifier);
        var factory = services.GetRequiredService<ITerminalSessionFactory>();
        var target = CreateTargetBehindJumpHost(cluster);
        var request = new TerminalSessionRequest(target, InitialSize, TerminalSessionRequest.DefaultTerminalType);

        var opened = await factory.OpenAsync(request, CancellationToken.None);
        await using var session = opened.IsSuccess ? opened.Value : null;
        opened.IsSuccess.Should().BeTrue(opened.Error?.Message);
        var conversation = new TerminalConversation(session!);
        await conversation.SendLineAsync("printf 'HOST[%s]\\n' \"$(hostname)\"");
        var hostName = await conversation.ExpectAsync(HostNamePattern);

        hostName.Groups[1].Value.Should().Be(cluster.Target.Identity.HostName);
        verifier.Received(1).Verify(Arg.Is<HostKeyVerificationRequest>(sent => sent.Endpoint == cluster.Jump.Endpoint && sent.HostKey == cluster.Jump.Identity.HostKey));
        verifier.Received(1).Verify(Arg.Is<HostKeyVerificationRequest>(sent => sent.Endpoint == target.Endpoint && sent.HostKey == cluster.Target.Identity.HostKey));
    }

    [TestMethod]
    public async Task ConnectSftp_ThroughJumpHost_ListsRemoteDirectory()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        var verifier = SshTransportTestServices.CreatePinnedVerifier(cluster.Target.Identity.HostKey, cluster.Jump.Identity.HostKey);
        await using var services = SshTransportTestServices.Create(verifier);
        var factory = services.GetRequiredService<IRemoteFileClientFactory>();
        var target = CreateTargetBehindJumpHost(cluster);

        var connected = await factory.ConnectAsync(target, CancellationToken.None);
        await using var client = connected.IsSuccess ? connected.Value : null;
        connected.IsSuccess.Should().BeTrue(connected.Error?.Message);
        var listResult = await client!.ListAsync("/", CancellationToken.None);

        listResult.IsSuccess.Should().BeTrue(listResult.Error?.Message);
        listResult.Value.Should().Contain(entry => entry.Name == "home");
    }

    [TestMethod]
    public async Task Connect_WhenJumpHostKeyIsRejected_ReportsJumpHostAndDoesNotReachTarget()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        var verifier = SshTransportTestServices.CreatePinnedVerifier(cluster.Target.Identity.HostKey);
        await using var services = SshTransportTestServices.Create(verifier);
        var factory = services.GetRequiredService<IRemoteFileClientFactory>();
        var target = CreateTargetBehindJumpHost(cluster);

        var result = await factory.ConnectAsync(target, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(SshErrorKind.HostKeyRejected);
        result.Error.Message.Should().StartWith($"Jump host {cluster.Jump.Endpoint}");
        result.Error.PresentedHostKey.Should().Be(cluster.Jump.Identity.HostKey);
        verifier.DidNotReceive().Verify(Arg.Is<HostKeyVerificationRequest>(sent => sent.Endpoint == target.Endpoint));
    }

    private static SshConnectionTarget CreateTargetBehindJumpHost(SshServerCluster cluster)
    {
        var jumpHost = cluster.Jump.CreateKeyTarget();
        return cluster.Target.CreateNetworkKeyTarget() with { JumpHost = jumpHost };
    }
}
