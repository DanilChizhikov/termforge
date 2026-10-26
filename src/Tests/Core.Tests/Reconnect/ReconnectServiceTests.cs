using System.Text;
using DTech.Core.Connections;
using DTech.Core.Reconnect;
using DTech.Core.SavedConnections;
using DTech.Core.Terminal;
using DTech.Core.Terminal.Sessions;
using DTech.Core.Tests.Reconnect.Support;
using DTech.Core.Tests.SavedConnections.Support;
using FluentAssertions;
using NSubstitute;

namespace DTech.Core.Tests.Reconnect;

[TestClass]
public sealed class ReconnectServiceTests
{
    private const string Owner = "owner-session-token";

    private static readonly TerminalSize Size = new(100, 30);

    private InMemorySavedConnectionRepository _repository = null!;
    private ITerminalSessionFactory _sessionFactory = null!;
    private List<ITerminalSession> _transports = null!;
    private TerminalSessionManager _manager = null!;
    private ManualTimeProvider _time = null!;
    private int _unreachableAttempts;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Initialize()
    {
        _repository = new InMemorySavedConnectionRepository();
        _transports = [];
        _time = new ManualTimeProvider();
        _sessionFactory = Substitute.For<ITerminalSessionFactory>();
        _sessionFactory.OpenAsync(default!, default).ReturnsForAnyArgs(_ => OpenOrFail());
        _manager = new TerminalSessionManager(_sessionFactory, TimeProvider.System, TerminalSessionOptions.Default);
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        await _manager.DisposeAsync();
    }

    [TestMethod]
    public async Task Reconnect_WhenPolicyIsOff_IsRejectedWithoutOpeningAShell()
    {
        var connection = await AddAsync(new ReconnectPolicy(ReconnectMode.Off, 5, 2, 60));

        var outcome = await CreateService().ReconnectAsync(new TerminalLaunchRequest(connection.Id, Owner, Size), TestContext.CancellationToken);

        outcome.IsSuccess.Should().BeFalse();
        outcome.Error!.Code.Should().Be(TerminalErrorCode.ReconnectDisabled);
        await _sessionFactory.DidNotReceiveWithAnyArgs().OpenAsync(default!, default);
    }

    [TestMethod]
    public async Task Reconnect_ForUnknownConnection_ReturnsNotFound()
    {
        var outcome = await CreateService().ReconnectAsync(new TerminalLaunchRequest(Guid.NewGuid(), Owner, Size), TestContext.CancellationToken);

        outcome.Error!.Code.Should().Be(TerminalErrorCode.NotFound);
    }

    [TestMethod]
    public async Task Reconnect_Manual_OpensOneFreshShellImmediately()
    {
        var connection = await AddAsync(new ReconnectPolicy(ReconnectMode.Manual, 5, 30, 60));

        var outcome = await CreateService().ReconnectAsync(new TerminalLaunchRequest(connection.Id, Owner, Size), TestContext.CancellationToken);

        outcome.IsSuccess.Should().BeTrue(outcome.Error?.Message);
        outcome.Attempts.Should().Be(1);
        _transports.Should().HaveCount(1);
        _time.PendingTimerCount.Should().Be(0);
    }

    [TestMethod]
    public async Task Reconnect_AfterTheOldShellDied_NeverReplaysInputIntoTheNewShell()
    {
        var connection = await AddAsync(new ReconnectPolicy(ReconnectMode.Manual, 5, 2, 60));
        var service = CreateService();
        var first = await service.ReconnectAsync(new TerminalLaunchRequest(connection.Id, Owner, Size), TestContext.CancellationToken);
        var typedCommand = Encoding.UTF8.GetBytes("rm -rf build\n");
        await first.Session!.WriteAsync(typedCommand, TestContext.CancellationToken);
        await _manager.ReleaseAsync(first.Session, TerminalCloseReasons.RemoteShellExited);

        var second = await service.ReconnectAsync(new TerminalLaunchRequest(connection.Id, Owner, Size), TestContext.CancellationToken);

        second.IsSuccess.Should().BeTrue(second.Error?.Message);
        second.Session!.Descriptor.Id.Should().NotBe(first.Session.Descriptor.Id);
        _transports.Should().HaveCount(2);
        await _transports[0].Received(1).WriteAsync(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>());
        await _transports[1].DidNotReceiveWithAnyArgs().WriteAsync(default, default);
    }

    [TestMethod]
    public async Task Reconnect_Automatic_RetriesUnreachableServerUsingBackoffUntilItIsBack()
    {
        var connection = await AddAsync(new ReconnectPolicy(ReconnectMode.Automatic, 3, 2, 60));
        _unreachableAttempts = 2;

        var run = CreateService().ReconnectAsync(new TerminalLaunchRequest(connection.Id, Owner, Size), TestContext.CancellationToken);
        await _time.WaitForPendingTimerAsync();
        _time.Advance(TimeSpan.FromSeconds(2));
        await _time.WaitForPendingTimerAsync();
        _time.Advance(TimeSpan.FromSeconds(4));
        await _time.WaitForPendingTimerAsync();
        _time.Advance(TimeSpan.FromSeconds(8));
        var outcome = await run;

        outcome.IsSuccess.Should().BeTrue(outcome.Error?.Message);
        outcome.Attempts.Should().Be(3);
    }

    private ReconnectService CreateService()
    {
        var passwordStore = new ConnectionPasswordStore(new InMemoryCredentialStore());
        var targetFactory = new ConnectionTargetFactory(passwordStore);
        var launcher = new TerminalLauncher(_repository, targetFactory, _manager);
        var scheduler = new ReconnectScheduler(_time);
        return new ReconnectService(_repository, launcher, scheduler);
    }

    private async Task<SavedConnection> AddAsync(ReconnectPolicy policy)
    {
        var draft = new SavedConnectionDraft
        {
            Name = "web",
            Host = "web.example.test",
            UserName = "deploy",
            AuthMethod = ConnectionAuthMethod.Agent,
            ReconnectPolicy = policy,
        };
        var connection = new SavedConnection { Id = Guid.NewGuid(), Details = draft };
        await _repository.AddAsync(connection, TestContext.CancellationToken);
        return connection;
    }

    private Task<SshResult<ITerminalSession>> OpenTransport()
    {
        var transport = Substitute.For<ITerminalSession>();
        transport.WriteAsync(default, default).ReturnsForAnyArgs(ValueTask.FromResult(SshResult.Success()));
        _transports.Add(transport);
        return Task.FromResult(SshResult<ITerminalSession>.Success(transport));
    }

    private Task<SshResult<ITerminalSession>> OpenOrFail()
    {
        if (_unreachableAttempts > 0)
        {
            _unreachableAttempts--;
            return UnreachableServer();
        }

        return OpenTransport();
    }

    private static Task<SshResult<ITerminalSession>> UnreachableServer()
    {
        var error = new SshError(SshErrorKind.ConnectionFailed, "unreachable");
        return Task.FromResult(SshResult<ITerminalSession>.Failure(error));
    }
}
