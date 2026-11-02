using System.Diagnostics;
using System.Text;
using DTech.Core.Connections;
using DTech.Core.Terminal;
using DTech.Ssh.Commands;
using DTech.Ssh.Connections;
using DTech.Ssh.IntegrationTests.Harness;
using DTech.Ssh.Terminal;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Renci.SshNet;

namespace DTech.Ssh.IntegrationTests;

[TestClass]
[DoNotParallelize]
public sealed class StreamingBacklogTests
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(15);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Terminal_StalledConsumer_ClosesYesAndBoundsLibraryBacklog()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        var verifier = SshTransportTestServices.CreatePinnedVerifier(cluster.Target.Identity.HostKey);
        await using var services = SshTransportTestServices.Create(verifier);
        var connector = services.GetRequiredService<SshClientConnector>();
        var opened = await connector.ConnectAsync(cluster.Target.CreateKeyTarget(), CreateClient, TestContext.CancellationToken);
        opened.IsSuccess.Should().BeTrue(opened.Error?.Message);
        using var connection = opened.Value;
        using var shell = connection.Client.CreateShellStream("xterm", 80, 24, 0, 0, 16 * 1024);
        await using var session = new SshNetTerminalSession(shell, connection);
        var command = "stty -echo; exec yes terminal-overflow\r"u8.ToArray();
        using var process = Process.GetCurrentProcess();
        var baseline = process.WorkingSet64;

        var sent = await session.WriteAsync(command, TestContext.CancellationToken);
        sent.IsSuccess.Should().BeTrue(sent.Error?.Message);
        var peak = baseline;
        var largestBuffer = 0L;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        timeout.CancelAfter(Deadline);
        while (session.Failure is null)
        {
            process.Refresh();
            peak = Math.Max(peak, process.WorkingSet64);
            largestBuffer = Math.Max(largestBuffer, shell.Length);
            await Task.Delay(5, timeout.Token);
        }

        session.Failure.Message.Should().Contain("output backlog");
        largestBuffer.Should().BeLessThan(32 * 1024 * 1024);
        (peak - baseline).Should().BeLessThan(128 * 1024 * 1024);
        var cleanup = session.DisposeAsync().AsTask();
        await cleanup.WaitAsync(Deadline, TestContext.CancellationToken);
        connection.Closed.IsCancellationRequested.Should().BeTrue();
        var useDisposedClient = () => connection.Client.IsConnected;
        useDisposedClient.Should().Throw<ObjectDisposedException>();
        TestContext.WriteLine($"Terminal peak buffer: {largestBuffer}; RSS delta: {peak - baseline}");
    }

    [TestMethod]
    public async Task Command_StalledConsumer_ClosesStdoutAndReportsOverflow()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        var verifier = SshTransportTestServices.CreatePinnedVerifier(cluster.Target.Identity.HostKey);
        await using var services = SshTransportTestServices.Create(verifier);
        var connector = services.GetRequiredService<SshClientConnector>();
        var opened = await connector.ConnectAsync(cluster.Target.CreateKeyTarget(), CreateClient, TestContext.CancellationToken);
        using var connection = opened.Value;
        using var command = connection.Client.CreateCommand("exec yes command-overflow");
        await using var stream = new SshNetRemoteCommandStream(connection, command);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        timeout.CancelAfter(Deadline);
        var largestBuffer = 0L;
        while (!connection.Closed.IsCancellationRequested)
        {
            largestBuffer = Math.Max(largestBuffer, command.OutputStream.Length);
            await Task.Delay(5, timeout.Token);
        }

        var buffer = new byte[16 * 1024];
        var readAll = async () =>
        {
            while (await stream.ReadAsync(buffer, timeout.Token) > 0)
            {
            }
        };
        var failure = await readAll.Should().ThrowAsync<IOException>();
        failure.Which.InnerException.Should().BeOfType<IOException>();
        largestBuffer.Should().BeLessThan(32 * 1024 * 1024);
        var cleanup = stream.DisposeAsync().AsTask();
        await cleanup.WaitAsync(Deadline, TestContext.CancellationToken);
        TestContext.WriteLine($"Command peak stdout buffer: {largestBuffer}");
    }

    [TestMethod]
    public async Task Command_StderrFlood_IsDrainedOrFailsExplicitlyWithoutUnboundedBuffering()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        var verifier = SshTransportTestServices.CreatePinnedVerifier(cluster.Target.Identity.HostKey);
        await using var services = SshTransportTestServices.Create(verifier);
        var connector = services.GetRequiredService<SshClientConnector>();
        var opened = await connector.ConnectAsync(cluster.Target.CreateKeyTarget(), CreateClient, TestContext.CancellationToken);
        using var connection = opened.Value;
        using var command = connection.Client.CreateCommand("head -c 33554432 /dev/zero >&2; printf done");
        await using var stream = new SshNetRemoteCommandStream(connection, command);
        var buffer = new byte[16 * 1024];
        using var output = new MemoryStream();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        timeout.CancelAfter(Deadline);
        var failed = false;
        try
        {
            var length = await stream.ReadAsync(buffer, timeout.Token);
            while (length > 0)
            {
                output.Write(buffer, 0, length);
                length = await stream.ReadAsync(buffer, timeout.Token);
            }
        }
        catch (IOException exception)
        {
            failed = true;
            exception.InnerException.Should().BeOfType<IOException>().Which.Message.Should().Contain("stderr backlog");
        }

        if (!failed)
        {
            Encoding.UTF8.GetString(output.ToArray()).Should().Be("done");
            stream.ExitCode.Should().Be(0);
        }

        command.ExtendedOutputStream.Length.Should().BeLessThan(32 * 1024 * 1024);
        var cleanup = stream.DisposeAsync().AsTask();
        await cleanup.WaitAsync(Deadline, TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task Command_DisposeWhileBothReadersAreBlocked_CompletesAllWorkers()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        var verifier = SshTransportTestServices.CreatePinnedVerifier(cluster.Target.Identity.HostKey);
        await using var services = SshTransportTestServices.Create(verifier);
        var connector = services.GetRequiredService<SshClientConnector>();
        var opened = await connector.ConnectAsync(cluster.Target.CreateKeyTarget(), CreateClient, TestContext.CancellationToken);
        using var connection = opened.Value;
        using var command = connection.Client.CreateCommand("sleep 60");
        await using var stream = new SshNetRemoteCommandStream(connection, command);

        var cleanup = stream.DisposeAsync().AsTask();
        await cleanup.WaitAsync(Deadline, TestContext.CancellationToken);

        connection.Closed.IsCancellationRequested.Should().BeTrue();
        var useDisposedClient = () => connection.Client.IsConnected;
        useDisposedClient.Should().Throw<ObjectDisposedException>();
    }

    [TestMethod]
    public async Task Command_DisposeOnBlackholedConnection_CompletesAllWorkers()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        await using var proxy = new BlackholeProxy(cluster.Target.Endpoint);
        var verifier = SshTransportTestServices.CreatePinnedVerifier(cluster.Target.Identity.HostKey);
        await using var services = SshTransportTestServices.Create(verifier);
        var connector = services.GetRequiredService<SshClientConnector>();
        var target = cluster.Target.CreateKeyTarget() with { Endpoint = proxy.Endpoint };
        var opened = await connector.ConnectAsync(target, CreateClient, TestContext.CancellationToken);
        using var connection = opened.Value;
        using var command = connection.Client.CreateCommand("sleep 60");
        await using var stream = new SshNetRemoteCommandStream(connection, command);
        proxy.DropTraffic();

        var cleanup = stream.DisposeAsync().AsTask();
        await cleanup.WaitAsync(Deadline, TestContext.CancellationToken);

        connection.Closed.IsCancellationRequested.Should().BeTrue();
    }

    private static SshClient CreateClient(ConnectionInfo info)
    {
        return new SshClient(info);
    }
}
