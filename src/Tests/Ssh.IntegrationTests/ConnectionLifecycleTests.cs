using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using DTech.Core.Connections;
using DTech.Core.Terminal;
using DTech.Core.Terminal.Sessions;
using DTech.Ssh.Connections;
using DTech.Ssh.IntegrationTests.Harness;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Renci.SshNet;

namespace DTech.Ssh.IntegrationTests;

[TestClass]
public sealed class ConnectionLifecycleTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);
    private static readonly SshConnectionHealthOptions FastHealth = new(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(300));
    private static readonly TerminalSize Size = new(80, 24);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void ConnectionHealth_DefaultsCheckEveryThirtySecondsWithATenSecondDeadline()
    {
        SshConnectionHealthOptions.Default.Interval.Should().Be(TimeSpan.FromSeconds(30));
        SshConnectionHealthOptions.Default.Timeout.Should().Be(TimeSpan.FromSeconds(10));
    }

    [TestMethod]
    public async Task Terminal_BlackholedConnection_EndsBlockedReadWithAFailureAndReleasesResources()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        await using var proxy = new BlackholeProxy(cluster.Target.Endpoint);
        var verifier = SshTransportTestServices.CreatePinnedVerifier(cluster.Target.Identity.HostKey);
        await using var services = SshTransportTestServices.Create(verifier, UseFastHealth);
        var factory = services.GetRequiredService<ITerminalSessionFactory>();
        var target = cluster.Target.CreateKeyTarget() with { Endpoint = proxy.Endpoint };
        var request = new TerminalSessionRequest(target, Size, TerminalSessionRequest.DefaultTerminalType);
        var opened = await factory.OpenAsync(request, TestContext.CancellationToken);
        opened.IsSuccess.Should().BeTrue(opened.Error?.Message);
        await using var session = opened.Value;
        var conversation = new TerminalConversation(session);
        var ready = new Regex("HEALTH_READY");
        await conversation.SendLineAsync("printf 'HEALTH_READY\\n'");
        await conversation.ExpectAsync(ready);

        proxy.DropTraffic();
        var closed = ReadUntilClosedAsync(session);
        await closed.WaitAsync(TestTimeout, TestContext.CancellationToken);

        session.IsOpen.Should().BeFalse();
        session.Failure.Should().NotBeNull();
        session.Failure!.Kind.Should().Be(SshErrorKind.ConnectionFailed);
        session.Failure.Message.Should().Contain("did not respond");
        var dispose = session.DisposeAsync().AsTask();
        await dispose.WaitAsync(TestTimeout, TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task Sftp_BlackholedConnection_IsDetectedWithoutAShellCommand()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        await using var proxy = new BlackholeProxy(cluster.Target.Endpoint);
        var verifier = SshTransportTestServices.CreatePinnedVerifier(cluster.Target.Identity.HostKey);
        await using var services = SshTransportTestServices.Create(verifier, UseFastHealth);
        var connector = services.GetRequiredService<SshClientConnector>();
        var target = cluster.Target.CreateKeyTarget() with { Endpoint = proxy.Endpoint };
        var opened = await connector.ConnectAsync(target, CreateSftpClient, TestContext.CancellationToken);
        opened.IsSuccess.Should().BeTrue(opened.Error?.Message);
        using var connection = opened.Value;

        proxy.DropTraffic();
        await WaitUntilAsync(() => connection.Closed.IsCancellationRequested);

        connection.Failure.Should().NotBeNull();
        connection.Failure!.Kind.Should().Be(SshErrorKind.ConnectionFailed);
        connection.Failure.Message.Should().Contain("did not respond");
    }

    [TestMethod]
    public async Task Dispose_WhileAProbeIsBlocked_CleansUpWithoutReportingAFailure()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        await using var proxy = new BlackholeProxy(cluster.Target.Endpoint);
        var verifier = SshTransportTestServices.CreatePinnedVerifier(cluster.Target.Identity.HostKey);
        var options = FastHealth with { Timeout = TestTimeout };
        await using var services = SshTransportTestServices.Create(verifier, configured => configured.AddSingleton(options));
        var connector = services.GetRequiredService<SshClientConnector>();
        var target = cluster.Target.CreateKeyTarget() with { Endpoint = proxy.Endpoint };
        var opened = await connector.ConnectAsync(target, CreateSftpClient, TestContext.CancellationToken);
        opened.IsSuccess.Should().BeTrue(opened.Error?.Message);
        using var connection = opened.Value;
        proxy.DropTraffic();
        await Task.Delay(200, TestContext.CancellationToken);

        connection.Dispose();

        connection.Closed.IsCancellationRequested.Should().BeTrue();
        connection.Failure.Should().BeNull();
    }

    [TestMethod]
    public async Task Terminal_TransportError_EndsReadAndPreservesFailureInManagedSession()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        await using var proxy = new BlackholeProxy(cluster.Target.Endpoint);
        var verifier = SshTransportTestServices.CreatePinnedVerifier(cluster.Target.Identity.HostKey);
        await using var services = SshTransportTestServices.Create(verifier);
        var factory = services.GetRequiredService<ITerminalSessionFactory>();
        await using var manager = new TerminalSessionManager(factory, TimeProvider.System, TerminalSessionOptions.Default);
        var target = cluster.Target.CreateKeyTarget() with { Endpoint = proxy.Endpoint };
        var server = new TerminalServer(Guid.NewGuid(), "web", target);
        var request = new TerminalOpenRequest("owner", server, Size);
        var opened = await manager.OpenAsync(request, TestContext.CancellationToken);
        opened.IsSuccess.Should().BeTrue(opened.Error?.Message);
        var session = opened.Value;
        session.TryAttach().Should().BeTrue();

        await proxy.DisposeAsync();
        var adapter = new ManagedTerminalSessionAdapter(session);
        var closed = ReadUntilClosedAsync(adapter);
        await closed.WaitAsync(TestTimeout, TestContext.CancellationToken);

        session.CloseReason.Should().StartWith("SSH connection lost:");
        var reason = session.CloseReason!;
        await manager.ReleaseAsync(session, reason);
        session.IsClosed.Should().BeTrue();
        manager.Count.Should().Be(0);
    }

    [TestMethod]
    public async Task Connect_ClientConstructorFailsAfterOpeningJump_ClosesTheForwardedListener()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        var verifier = SshTransportTestServices.CreatePinnedVerifier(cluster.Target.Identity.HostKey, cluster.Jump.Identity.HostKey);
        await using var services = SshTransportTestServices.Create(verifier);
        var connector = services.GetRequiredService<SshClientConnector>();
        var jumpHost = cluster.Jump.CreateKeyTarget();
        var target = cluster.Target.CreateNetworkKeyTarget() with { JumpHost = jumpHost };
        IPEndPoint? forwarded = null;
        SftpClient FailClient(ConnectionInfo info)
        {
            var address = IPAddress.Parse(info.Host);
            forwarded = new IPEndPoint(address, info.Port);
            throw new NotSupportedException("client creation failed");
        }

        var result = await connector.ConnectAsync(target, FailClient, TestContext.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(SshErrorKind.OperationFailed);
        forwarded.Should().NotBeNull();
        await AssertListenerClosedAsync(forwarded!);
    }

    [TestMethod]
    public async Task Connect_CancelledAfterOpeningJump_ClosesTheForwardedListener()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        var verifier = SshTransportTestServices.CreatePinnedVerifier(cluster.Target.Identity.HostKey, cluster.Jump.Identity.HostKey);
        await using var services = SshTransportTestServices.Create(verifier);
        var connector = services.GetRequiredService<SshClientConnector>();
        var jumpHost = cluster.Jump.CreateKeyTarget();
        var target = cluster.Target.CreateNetworkKeyTarget() with { JumpHost = jumpHost };
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        IPEndPoint? forwarded = null;
        SftpClient CancelConnection(ConnectionInfo info)
        {
            var address = IPAddress.Parse(info.Host);
            forwarded = new IPEndPoint(address, info.Port);
            cancelled.Cancel();
            return new SftpClient(info);
        }

        var result = await connector.ConnectAsync(target, CancelConnection, cancelled.Token);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(SshErrorKind.Cancelled);
        forwarded.Should().NotBeNull();
        await AssertListenerClosedAsync(forwarded!);
    }

    [TestMethod]
    public async Task Authentication_MalformedAgentAddress_IsReportedAsAgentUnavailable()
    {
        var factory = new SshAuthenticationFactory();
        var authentication = new AgentAuthentication("broken\0agent");
        var endpoint = new SshEndpoint("127.0.0.1", 22);
        var target = new SshConnectionTarget(endpoint, "tester", authentication);

        var result = await factory.CreateAsync(target, TestContext.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(SshErrorKind.AgentUnavailable);
    }

    [TestMethod]
    public async Task Connect_KeyBecomesUnusableWhileConfirmingTarget_ClosesTheForwardedListener()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        var directory = TestKeyFiles.CreateDirectory();
        try
        {
            var path = Path.Combine(directory, "target_key");
            var original = await File.ReadAllBytesAsync(cluster.Target.Identity.PrivateKeyPath, TestContext.CancellationToken);
            await TestKeyFiles.WritePrivateKeyAsync(path, original, TestContext.CancellationToken);
            var malformed = "-----BEGIN RSA PRIVATE KEY-----\nMAA=\n-----END RSA PRIVATE KEY-----\n"u8.ToArray();
            var prompt = Substitute.For<IHostKeyTrustPrompt>();
            prompt.AskAsync(Arg.Any<HostKeyChallenge>(), Arg.Any<CancellationToken>()).Returns(async _ =>
            {
                await TestKeyFiles.WritePrivateKeyAsync(path, malformed, TestContext.CancellationToken);
                return HostKeyPromptOutcome.Accepted;
            });
            var verifier = SshTransportTestServices.CreatePinnedVerifier(cluster.Jump.Identity.HostKey);
            await using var services = SshTransportTestServices.Create(verifier, configured => configured.AddSingleton(prompt));
            var connector = services.GetRequiredService<SshClientConnector>();
            var jumpHost = cluster.Jump.CreateKeyTarget();
            var authentication = new PrivateKeyAuthentication(path, null);
            var target = cluster.Target.CreateNetworkKeyTarget() with { JumpHost = jumpHost, Authentication = authentication };
            IPEndPoint? forwarded = null;
            SftpClient RememberEndpoint(ConnectionInfo info)
            {
                var address = IPAddress.Parse(info.Host);
                forwarded = new IPEndPoint(address, info.Port);
                return new SftpClient(info);
            }

            var result = await connector.ConnectAsync(target, RememberEndpoint, TestContext.CancellationToken);

            result.IsSuccess.Should().BeFalse();
            result.Error!.Kind.Should().Be(SshErrorKind.PrivateKeyUnusable);
            forwarded.Should().NotBeNull();
            await AssertListenerClosedAsync(forwarded!);
        }
        finally
        {
            TestKeyFiles.DeleteDirectory(directory);
        }
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void Cleanup_AResourceThrows_StillDisposesEveryDependency(bool primaryThrows)
    {
        var primary = Substitute.For<IDisposable>();
        var first = Substitute.For<IDisposable>();
        var second = Substitute.For<IDisposable>();
        var failing = primaryThrows ? primary : first;
        failing.When(resource => resource.Dispose()).Do(_ => throw new IOException("cleanup failed"));
        var dependencies = new[] { first, second };
        var cleanup = () => SshResourceCleanup.Dispose(primary, dependencies);

        cleanup.Should().Throw<AggregateException>();
        primary.Received(1).Dispose();
        first.Received(1).Dispose();
        second.Received(1).Dispose();
    }

    private async Task ReadUntilClosedAsync(ITerminalSession session)
    {
        var buffer = new byte[16 * 1024];
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        timeout.CancelAfter(TestTimeout);
        while (await session.ReadAsync(buffer, timeout.Token) != 0)
        {
        }
    }

    private async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        timeout.CancelAfter(TestTimeout);
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    private async Task AssertListenerClosedAsync(IPEndPoint endpoint)
    {
        using var client = new TcpClient();
        var connect = async () => await client.ConnectAsync(endpoint.Address, endpoint.Port, TestContext.CancellationToken);

        await connect.Should().ThrowAsync<SocketException>();
    }

    private static void UseFastHealth(IServiceCollection services)
    {
        services.AddSingleton(FastHealth);
    }

    private static SftpClient CreateSftpClient(ConnectionInfo info)
    {
        return new SftpClient(info);
    }
}
