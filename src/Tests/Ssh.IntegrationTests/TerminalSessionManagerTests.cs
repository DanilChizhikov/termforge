using System.Text.RegularExpressions;
using DTech.Core.Terminal;
using DTech.Core.Terminal.Sessions;
using DTech.Ssh.IntegrationTests.Harness;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace DTech.Ssh.IntegrationTests;

[TestClass]
public sealed class TerminalSessionManagerTests
{
    private const string Owner = "browser-session-token";
    private const string PrintSizeCommand = "printf 'SIZE[%s]\\n' \"$(stty size)\"";
    private const string PrintShellCommand = "printf 'SHELL_PID[%s] TTY[%s]\\n' \"$$\" \"$(tty)\"";

    private static readonly TerminalSize InitialSize = new(80, 24);
    private static readonly Regex SizePattern = new(@"SIZE\[(\d+) (\d+)\]");
    private static readonly Regex ShellPattern = new(@"SHELL_PID\[(\d+)\] TTY\[(/dev/pts/\d+)\]");
    private static readonly Regex EchoPattern = new(@"ECHO\[([a-z0-9-]+)\]");
    private static readonly TimeSpan ReleaseTimeout = TimeSpan.FromSeconds(20);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task OpenedSession_ExchangesInputAndOutputAndResizeReachesRemotePty()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        await using var services = CreateServices(cluster);
        await using var manager = CreateManager(services);
        var opened = await OpenAsync(manager, cluster);
        var conversation = CreateConversation(opened);

        await conversation.SendLineAsync("echo \"ECHO[$((6 * 7))-answer]\"");
        var echoed = await conversation.ExpectAsync(EchoPattern);
        await conversation.SendLineAsync(PrintSizeCommand);
        var initialSize = await conversation.ExpectAsync(SizePattern);
        var resized = opened.Resize(new TerminalSize(132, 40));
        await conversation.SendLineAsync(PrintSizeCommand);
        var changedSize = await conversation.ExpectAsync(SizePattern);

        echoed.Groups[1].Value.Should().Be("42-answer");
        initialSize.Value.Should().Be("SIZE[24 80]");
        resized.IsSuccess.Should().BeTrue(resized.Error?.Message);
        changedSize.Value.Should().Be("SIZE[40 132]");
    }

    [TestMethod]
    public async Task ConcurrentSessions_HaveSeparatePtysAndShellState()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        await using var services = CreateServices(cluster);
        await using var manager = CreateManager(services);
        var first = await OpenAsync(manager, cluster);
        var second = await OpenAsync(manager, cluster);
        var firstConversation = CreateConversation(first);
        var secondConversation = CreateConversation(second);

        await firstConversation.SendLineAsync("MARKER=first-only");
        second.Resize(new TerminalSize(100, 30));
        await firstConversation.SendLineAsync(PrintShellCommand);
        await secondConversation.SendLineAsync(PrintShellCommand);
        var firstShell = await firstConversation.ExpectAsync(ShellPattern);
        var secondShell = await secondConversation.ExpectAsync(ShellPattern);
        await firstConversation.SendLineAsync(PrintSizeCommand);
        await secondConversation.SendLineAsync(PrintSizeCommand);
        var firstSize = await firstConversation.ExpectAsync(SizePattern);
        var secondSize = await secondConversation.ExpectAsync(SizePattern);
        await secondConversation.SendLineAsync("echo \"ECHO[${MARKER:-unset}]\"");
        var secondMarker = await secondConversation.ExpectAsync(EchoPattern);

        firstShell.Groups[2].Value.Should().NotBe(secondShell.Groups[2].Value);
        firstSize.Value.Should().Be("SIZE[24 80]");
        secondSize.Value.Should().Be("SIZE[30 100]");
        secondMarker.Groups[1].Value.Should().Be("unset");
        first.Descriptor.SessionNumber.Should().Be(1);
        second.Descriptor.SessionNumber.Should().Be(2);
    }

    [TestMethod]
    public async Task Close_ReleasesTheRemoteShell()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        await using var services = CreateServices(cluster);
        await using var manager = CreateManager(services);
        var opened = await OpenAsync(manager, cluster);
        var shellPid = await ReadShellPidAsync(opened);

        var closed = await manager.CloseAsync(opened.Descriptor.Id, Owner, TerminalCloseReasons.ClosedByUser);
        var isReleased = await WaitUntilProcessExitsAsync(cluster, shellPid);

        closed.Should().BeTrue();
        opened.IsClosed.Should().BeTrue();
        manager.Count.Should().Be(0);
        isReleased.Should().BeTrue($"remote shell {shellPid} should exit after the terminal is closed");
    }

    [TestMethod]
    public async Task Shutdown_ClosesEveryRemoteShell()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        await using var services = CreateServices(cluster);
        var manager = CreateManager(services);
        var first = await OpenAsync(manager, cluster);
        var second = await OpenAsync(manager, cluster);
        var firstPid = await ReadShellPidAsync(first);
        var secondPid = await ReadShellPidAsync(second);

        await manager.DisposeAsync();
        var firstReleased = await WaitUntilProcessExitsAsync(cluster, firstPid);
        var secondReleased = await WaitUntilProcessExitsAsync(cluster, secondPid);

        first.CloseReason.Should().Be(TerminalCloseReasons.ShuttingDown);
        second.CloseReason.Should().Be(TerminalCloseReasons.ShuttingDown);
        firstReleased.Should().BeTrue();
        secondReleased.Should().BeTrue();
    }

    [TestMethod]
    public async Task RemoteExit_EndsOutputSoTheSessionCanBeReleased()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        await using var services = CreateServices(cluster);
        await using var manager = CreateManager(services);
        var opened = await OpenAsync(manager, cluster);
        var conversation = CreateConversation(opened);

        await conversation.SendLineAsync("exit");
        await conversation.WaitForCloseAsync();
        await manager.ReleaseAsync(opened, TerminalCloseReasons.RemoteShellExited);

        opened.CloseReason.Should().Be(TerminalCloseReasons.RemoteShellExited);
        manager.Count.Should().Be(0);
    }

    private static ServiceProvider CreateServices(SshServerCluster cluster)
    {
        var verifier = SshTransportTestServices.CreatePinnedVerifier(cluster.Target.Identity.HostKey);
        return SshTransportTestServices.Create(verifier);
    }

    private static TerminalSessionManager CreateManager(ServiceProvider services)
    {
        var factory = services.GetRequiredService<ITerminalSessionFactory>();
        return new TerminalSessionManager(factory, TimeProvider.System, TerminalSessionOptions.Default);
    }

    private async Task<ManagedTerminalSession> OpenAsync(TerminalSessionManager manager, SshServerCluster cluster)
    {
        var target = cluster.Target.CreateKeyTarget();
        var server = new TerminalServer(Guid.Empty, "target", target);
        var request = new TerminalOpenRequest(Owner, server, InitialSize);
        var opened = await manager.OpenAsync(request, TestContext.CancellationToken);
        opened.IsSuccess.Should().BeTrue(opened.Error?.Message);
        opened.Value.TryAttach();
        return opened.Value;
    }

    private static TerminalConversation CreateConversation(ManagedTerminalSession session)
    {
        var adapter = new ManagedTerminalSessionAdapter(session);
        return new TerminalConversation(adapter);
    }

    private static async Task<string> ReadShellPidAsync(ManagedTerminalSession session)
    {
        var conversation = CreateConversation(session);
        await conversation.SendLineAsync(PrintShellCommand);
        var shell = await conversation.ExpectAsync(ShellPattern);
        return shell.Groups[1].Value;
    }

    private async Task<bool> WaitUntilProcessExitsAsync(SshServerCluster cluster, string pid)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        timeout.CancelAfter(ReleaseTimeout);
        var probe = $"if kill -0 {pid} 2>/dev/null; then echo ALIVE; else echo GONE; fi";
        try
        {
            while (true)
            {
                var state = await cluster.Target.ExecuteAsync(probe, timeout.Token);
                if (state == "GONE")
                {
                    return true;
                }

                await Task.Delay(200, timeout.Token);
            }
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
