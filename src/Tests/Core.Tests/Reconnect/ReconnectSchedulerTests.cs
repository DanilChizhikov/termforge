using DTech.Core.Connections;
using DTech.Core.Reconnect;
using DTech.Core.Terminal;
using DTech.Core.Terminal.Sessions;
using DTech.Core.Tests.Reconnect.Support;
using FluentAssertions;
using NSubstitute;

namespace DTech.Core.Tests.Reconnect;

[TestClass]
public sealed class ReconnectSchedulerTests
{
    private static readonly SshConnectionTarget Target = new(new SshEndpoint("example.test", 22), "deploy", new AgentAuthentication(null));

    private ManualTimeProvider _time = null!;
    private ReconnectScheduler _scheduler = null!;
    private List<DateTimeOffset> _attemptTimes = null!;
    private TerminalSessionManager _manager = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Initialize()
    {
        _time = new ManualTimeProvider();
        _scheduler = new ReconnectScheduler(_time);
        _attemptTimes = [];
        var factory = Substitute.For<ITerminalSessionFactory>();
        var transport = Substitute.For<ITerminalSession>();
        factory.OpenAsync(default!, default).ReturnsForAnyArgs(SshResult<ITerminalSession>.Success(transport));
        _manager = new TerminalSessionManager(factory, TimeProvider.System, TerminalSessionOptions.Default);
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        await _manager.DisposeAsync();
    }

    [TestMethod]
    public async Task Automatic_WaitsForExponentialBackoffBeforeEveryAttempt()
    {
        var policy = new ReconnectPolicy(ReconnectMode.Automatic, 3, 2, 60);
        var opened = await OpenSessionAsync();
        var results = new Queue<TerminalResult<ManagedTerminalSession>>(
        [
            Failure(SshErrorKind.ConnectionFailed),
            Failure(SshErrorKind.ConnectionFailed),
            opened,
        ]);

        var run = _scheduler.RunAsync(policy, token => OpenNext(results), TestContext.CancellationToken);
        await _time.WaitForPendingTimerAsync();
        run.IsCompleted.Should().BeFalse();
        _time.Advance(TimeSpan.FromSeconds(2));
        await _time.WaitForPendingTimerAsync();
        _time.Advance(TimeSpan.FromSeconds(4));
        await _time.WaitForPendingTimerAsync();
        _attemptTimes.Should().HaveCount(2);
        _time.Advance(TimeSpan.FromSeconds(8));
        var outcome = await run;

        outcome.IsSuccess.Should().BeTrue();
        outcome.Attempts.Should().Be(3);
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        _attemptTimes.Should().Equal(start + TimeSpan.FromSeconds(2), start + TimeSpan.FromSeconds(6), start + TimeSpan.FromSeconds(14));
    }

    [TestMethod]
    public async Task Automatic_StopsAfterAttemptLimitAndReportsLastError()
    {
        var policy = new ReconnectPolicy(ReconnectMode.Automatic, 2, 1, 5);
        var calls = 0;
        Task<TerminalResult<ManagedTerminalSession>> AlwaysUnreachable(CancellationToken token)
        {
            calls++;
            return Task.FromResult(Failure(SshErrorKind.ConnectionFailed));
        }

        var run = _scheduler.RunAsync(policy, AlwaysUnreachable, TestContext.CancellationToken);
        await _time.WaitForPendingTimerAsync();
        _time.Advance(TimeSpan.FromSeconds(1));
        await _time.WaitForPendingTimerAsync();
        _time.Advance(TimeSpan.FromSeconds(2));
        var outcome = await run;

        outcome.IsSuccess.Should().BeFalse();
        outcome.Attempts.Should().Be(2);
        calls.Should().Be(2);
        outcome.Error!.Code.Should().Be(TerminalErrorCode.ReconnectFailed);
        outcome.Error.Message.Should().Contain("2 attempt");
    }

    [TestMethod]
    [DataRow(SshErrorKind.AuthenticationFailed)]
    [DataRow(SshErrorKind.HostKeyChanged)]
    [DataRow(SshErrorKind.HostKeyRejected)]
    public async Task Automatic_DoesNotRetryPermanentFailures(SshErrorKind kind)
    {
        var policy = new ReconnectPolicy(ReconnectMode.Automatic, 5, 1, 5);
        var calls = 0;
        Task<TerminalResult<ManagedTerminalSession>> Rejected(CancellationToken token)
        {
            calls++;
            return Task.FromResult(Failure(kind));
        }

        var run = _scheduler.RunAsync(policy, Rejected, TestContext.CancellationToken);
        await _time.WaitForPendingTimerAsync();
        _time.Advance(TimeSpan.FromSeconds(1));
        var outcome = await run;

        calls.Should().Be(1);
        outcome.Error!.SshErrorKind.Should().Be(kind);
        outcome.Attempts.Should().Be(1);
    }

    [TestMethod]
    public async Task Manual_AttemptsOnceImmediatelyWithoutWaiting()
    {
        var policy = new ReconnectPolicy(ReconnectMode.Manual, 5, 30, 60);
        var results = new Queue<TerminalResult<ManagedTerminalSession>>([Failure(SshErrorKind.ConnectionFailed)]);

        var outcome = await _scheduler.RunAsync(policy, token => OpenNext(results), TestContext.CancellationToken);

        outcome.IsSuccess.Should().BeFalse();
        outcome.Attempts.Should().Be(1);
        _time.PendingTimerCount.Should().Be(0);
    }

    [TestMethod]
    public async Task Automatic_CancellationDuringBackoffStopsWithoutAttempting()
    {
        var policy = new ReconnectPolicy(ReconnectMode.Automatic, 5, 30, 60);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var calls = 0;
        var opened = await OpenSessionAsync();
        Task<TerminalResult<ManagedTerminalSession>> Counted(CancellationToken token)
        {
            calls++;
            return Task.FromResult(opened);
        }

        var run = _scheduler.RunAsync(policy, Counted, cancellation.Token);
        await _time.WaitForPendingTimerAsync();
        await cancellation.CancelAsync();
        var act = async () => await run;

        await act.Should().ThrowAsync<OperationCanceledException>();
        calls.Should().Be(0);
    }

    private Task<TerminalResult<ManagedTerminalSession>> OpenNext(Queue<TerminalResult<ManagedTerminalSession>> results)
    {
        _attemptTimes.Add(_time.GetUtcNow());
        return Task.FromResult(results.Dequeue());
    }

    private static TerminalResult<ManagedTerminalSession> Failure(SshErrorKind kind)
    {
        var error = TerminalError.FromSsh(new SshError(kind, kind.ToString()));
        return TerminalResult<ManagedTerminalSession>.Failure(error);
    }

    private async Task<TerminalResult<ManagedTerminalSession>> OpenSessionAsync()
    {
        var server = new TerminalServer(Guid.NewGuid(), "web", Target);
        var request = new TerminalOpenRequest("owner", server, new TerminalSize(80, 24));
        return await _manager.OpenAsync(request, TestContext.CancellationToken);
    }
}
