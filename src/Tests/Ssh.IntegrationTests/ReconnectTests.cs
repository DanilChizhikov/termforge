using System.Text.RegularExpressions;
using DTech.Core.Reconnect;
using DTech.Core.SavedConnections;
using DTech.Core.Terminal;
using DTech.Core.Terminal.Sessions;
using DTech.Ssh.IntegrationTests.Harness;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace DTech.Ssh.IntegrationTests;

[TestClass]
public sealed class ReconnectTests
{
    private const string Owner = "browser-session-token";
    private const string ReplayMarker = "REPLAY-MARKER-4711";

    private static readonly TerminalSize Size = new(80, 24);
    private static readonly Regex ShellPattern = new(@"SHELL_PID\[(\d+)\]");
    private static readonly Regex EchoPattern = new(@"ECHO\[([a-z0-9-]+)\]");

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Reconnect_AfterServerSideShellIsKilled_OpensAFreshShellAndReplaysNothing()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        var verifier = SshTransportTestServices.CreatePinnedVerifier(cluster.Target.Identity.HostKey);
        await using var services = SshTransportTestServices.Create(verifier);
        var factory = services.GetRequiredService<ITerminalSessionFactory>();
        await using var manager = new TerminalSessionManager(factory, TimeProvider.System, TerminalSessionOptions.Default);
        var connectionId = Guid.NewGuid();
        var service = CreateService(manager, cluster, connectionId);
        var launch = new TerminalLaunchRequest(connectionId, Owner, Size);

        var first = await service.ReconnectAsync(launch, TestContext.CancellationToken);
        first.IsSuccess.Should().BeTrue(first.Error?.Message);
        first.Session!.TryAttach();
        var firstConversation = new TerminalConversation(new ManagedTerminalSessionAdapter(first.Session));
        await firstConversation.SendLineAsync($"echo {ReplayMarker}");
        await firstConversation.SendLineAsync("printf 'SHELL_PID[%s]\\n' \"$$\"");
        var firstPid = (await firstConversation.ExpectAsync(ShellPattern)).Groups[1].Value;

        await cluster.Target.ExecuteAsync($"kill -9 {firstPid}", TestContext.CancellationToken);
        await firstConversation.WaitForCloseAsync();
        await manager.ReleaseAsync(first.Session, TerminalCloseReasons.RemoteShellExited);

        var second = await service.ReconnectAsync(launch, TestContext.CancellationToken);
        second.IsSuccess.Should().BeTrue(second.Error?.Message);
        second.Session!.TryAttach();
        var secondConversation = new TerminalConversation(new ManagedTerminalSessionAdapter(second.Session));
        await secondConversation.SendLineAsync("printf 'SHELL_PID[%s]\\n' \"$$\"");
        var secondPid = (await secondConversation.ExpectAsync(ShellPattern)).Groups[1].Value;
        await secondConversation.SendLineAsync("echo \"ECHO[$((6 * 7))-fresh]\"");
        var echoed = await secondConversation.ExpectAsync(EchoPattern);

        secondPid.Should().NotBe(firstPid);
        echoed.Groups[1].Value.Should().Be("42-fresh");
        second.Session.Descriptor.Id.Should().NotBe(first.Session.Descriptor.Id);
        secondConversation.Transcript.Should().NotContain($"ECHO[{ReplayMarker}]");
        Regex.Matches(secondConversation.Transcript, ReplayMarker).Should().BeEmpty();
    }

    private static ReconnectService CreateService(TerminalSessionManager manager, SshServerCluster cluster, Guid connectionId)
    {
        var draft = new SavedConnectionDraft
        {
            Name = "target",
            Host = "localhost",
            UserName = "tester",
            ReconnectPolicy = new ReconnectPolicy(ReconnectMode.Manual, 3, 1, 5),
        };
        var connection = new SavedConnection { Id = connectionId, Details = draft };
        var repository = Substitute.For<ISavedConnectionRepository>();
        repository.FindAsync(connectionId, Arg.Any<CancellationToken>()).Returns(connection);
        var launcher = new ClusterTerminalLauncher(manager, cluster);
        var scheduler = new ReconnectScheduler(TimeProvider.System);
        return new ReconnectService(repository, launcher, scheduler);
    }

    private sealed class ClusterTerminalLauncher(TerminalSessionManager manager, SshServerCluster cluster) : ITerminalLauncher
    {
        public Task<TerminalResult<ManagedTerminalSession>> LaunchAsync(TerminalLaunchRequest request, CancellationToken cancellationToken)
        {
            var target = cluster.Target.CreateKeyTarget();
            var server = new TerminalServer(request.ConnectionId, "target", target);
            var openRequest = new TerminalOpenRequest(request.OwnerKey, server, request.InitialSize);
            return manager.OpenAsync(openRequest, cancellationToken);
        }
    }
}
