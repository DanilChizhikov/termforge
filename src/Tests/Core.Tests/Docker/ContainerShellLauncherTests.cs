using System.Text;
using DTech.Core.Connections;
using DTech.Core.Docker;
using DTech.Core.SavedConnections;
using DTech.Core.Terminal;
using DTech.Core.Terminal.Sessions;
using DTech.Core.Tests.Docker.Support;
using FluentAssertions;
using NSubstitute;

namespace DTech.Core.Tests.Docker;

[TestClass]
public sealed class ContainerShellLauncherTests
{
    private const string Owner = "owner-session-token";

    private static readonly TerminalSize Size = new(100, 30);

    private DockerTestGraph _graph = null!;
    private ITerminalSession _terminal = null!;
    private ITerminalSessionFactory _sessionFactory = null!;
    private TerminalSessionManager _manager = null!;
    private List<string> _written = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Initialize()
    {
        _graph = new DockerTestGraph();
        _written = [];
        _terminal = Substitute.For<ITerminalSession>();
        _terminal.WriteAsync(default, default).ReturnsForAnyArgs(callInfo =>
        {
            var data = callInfo.Arg<ReadOnlyMemory<byte>>();
            var text = Encoding.UTF8.GetString(data.Span);
            _written.Add(text);
            var success = SshResult.Success();
            return ValueTask.FromResult(success);
        });
        _sessionFactory = Substitute.For<ITerminalSessionFactory>();
        var opened = SshResult<ITerminalSession>.Success(_terminal);
        _sessionFactory.OpenAsync(default!, default).ReturnsForAnyArgs(opened);
        _manager = new TerminalSessionManager(_sessionFactory, TimeProvider.System, TerminalSessionOptions.Default);
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        await _manager.DisposeAsync();
    }

    [TestMethod]
    public async Task Launch_OpensTerminalSessionAndExecsIntoQuotedContainerWithFallback()
    {
        var connection = await _graph.AddConnectionAsync("web", TestContext.CancellationToken);
        var launch = new TerminalLaunchRequest(connection.Id, Owner, Size);
        var request = new ContainerShellRequest(launch, "api_1", ContainerShell.Auto);

        var launched = await CreateLauncher().LaunchAsync(request, TestContext.CancellationToken);

        launched.IsSuccess.Should().BeTrue(launched.Error?.Message);
        launched.Value.Descriptor.ConnectionId.Should().Be(connection.Id);
        _manager.List(Owner).Should().ContainSingle();
        var line = _written.Should().ContainSingle().Subject;
        line.Should().Contain("exec docker exec -it 'api_1' sh -c 'if command -v bash");
        line.Should().Contain("exec sh");
    }

    [TestMethod]
    public async Task Launch_WithUnsafeContainer_IsRejectedBeforeOpeningTerminal()
    {
        var connection = await _graph.AddConnectionAsync("web", TestContext.CancellationToken);
        var launch = new TerminalLaunchRequest(connection.Id, Owner, Size);
        var request = new ContainerShellRequest(launch, "api$(reboot)", ContainerShell.Bash);

        var launched = await CreateLauncher().LaunchAsync(request, TestContext.CancellationToken);

        launched.Error!.Code.Should().Be(TerminalErrorCode.InvalidRequest);
        await _sessionFactory.DidNotReceiveWithAnyArgs().OpenAsync(default!, default);
    }

    [TestMethod]
    public async Task Launch_WhenStartupWriteFails_ReleasesSession()
    {
        var connection = await _graph.AddConnectionAsync("web", TestContext.CancellationToken);
        var writeError = new SshError(SshErrorKind.SessionClosed, "closed");
        var failedWrite = SshResult.Failure(writeError);
        _terminal.WriteAsync(default, default).ReturnsForAnyArgs(ValueTask.FromResult(failedWrite));
        var launch = new TerminalLaunchRequest(connection.Id, Owner, Size);
        var request = new ContainerShellRequest(launch, "web", ContainerShell.Sh);

        var launched = await CreateLauncher().LaunchAsync(request, TestContext.CancellationToken);

        launched.IsSuccess.Should().BeFalse();
        _manager.List(Owner).Should().BeEmpty();
    }

    private ContainerShellLauncher CreateLauncher()
    {
        var terminalLauncher = new TerminalLauncher(_graph.Connections, _graph.TargetFactory, _manager);
        return new ContainerShellLauncher(terminalLauncher, _manager);
    }
}
