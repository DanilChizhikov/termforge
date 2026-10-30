using DTech.Core.AiBridge;
using DTech.Core.AiBridge.Approvals;
using DTech.Core.AiBridge.Commands;
using DTech.Core.Diagnostics;
using DTech.Core.Files;
using DTech.Core.Files.Search;
using DTech.Core.Operations;
using DTech.Core.Tests.AiBridge.Support;
using FluentAssertions;
using NSubstitute;

namespace DTech.Core.Tests.AiBridge;

[TestClass]
public sealed class AiCommandServiceTests
{
    private AiBridgeTestGraph _graph = null!;
    private IRemoteCommandRunnerOpener _runnerOpener = null!;
    private AiCommandService _service = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Initialize()
    {
        _graph = new AiBridgeTestGraph();
        _runnerOpener = Substitute.For<IRemoteCommandRunnerOpener>();
        _runnerOpener.OpenAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(_ => FileResult<IRemoteCommandRunner>.Success(_graph.Docker.Runner));
        _service = new AiCommandService(_graph.AccessPolicy, _graph.Gate, _runnerOpener, _graph.Recorder);
    }

    [TestMethod]
    public async Task Run_Approved_ReturnsOutputAndJournalsTheCommand()
    {
        var server = await _graph.AddServerAsync("prod-web", TestContext.CancellationToken);
        _graph.Docker.Runner.Respond("uptime", 0, "up 3 days", "warning");

        var request = new AiCommandRequest(server.Id, "uptime", null);
        var executed = await _service.RunAsync(request, TestContext.CancellationToken);

        executed.Value.ExitCode.Should().Be(0);
        executed.Value.StandardOutput.Should().Be("up 3 days");
        executed.Value.StandardError.Should().Be("warning");
        _graph.Notifier.Requested.Should().ContainSingle().Which.Should().Be(new AiActionDescription(server.Id, "prod-web", AiActionKind.Command, "uptime"));
        var operation = (await _graph.ListOperationsAsync(TestContext.CancellationToken)).Should().ContainSingle().Subject;
        operation.Kind.Should().Be(AiOperationRecorder.CommandKind);
        operation.Server.Should().Be("prod-web");
        operation.Status.Should().Be(OperationStatus.Succeeded);
        operation.Details.Should().Contain("command", "uptime").And.Contain("exitCode", "0");
        operation.Details.Values.Should().NotContain("up 3 days");
    }

    [TestMethod]
    public async Task Run_NonZeroExit_ReturnsResultAndJournalsFailure()
    {
        var server = await _graph.AddServerAsync("prod-web", TestContext.CancellationToken);
        _graph.Docker.Runner.Respond("false", 1);

        var request = new AiCommandRequest(server.Id, "false", null);
        var executed = await _service.RunAsync(request, TestContext.CancellationToken);

        executed.Value.ExitCode.Should().Be(1);
        var operation = (await _graph.ListOperationsAsync(TestContext.CancellationToken)).Should().ContainSingle().Subject;
        operation.Status.Should().Be(OperationStatus.Failed);
    }

    [TestMethod]
    public async Task Run_Denied_DoesNotConnectOrJournal()
    {
        var server = await _graph.AddServerAsync("prod-web", TestContext.CancellationToken);
        _graph.Notifier.Answer = AiApprovalOutcome.Denied;

        var request = new AiCommandRequest(server.Id, "reboot", null);
        var executed = await _service.RunAsync(request, TestContext.CancellationToken);

        executed.Error!.Code.Should().Be(AiBridgeErrorCode.Denied);
        await _runnerOpener.DidNotReceiveWithAnyArgs().OpenAsync(default, default);
        (await _graph.ListOperationsAsync(TestContext.CancellationToken)).Should().BeEmpty();
    }

    [TestMethod]
    public async Task Run_ReadOnlyServer_FailsBeforeAskingTheUser()
    {
        var server = await _graph.AddReadOnlyServerAsync("prod-db", TestContext.CancellationToken);

        var request = new AiCommandRequest(server.Id, "uptime", null);
        var executed = await _service.RunAsync(request, TestContext.CancellationToken);

        executed.Error!.Code.Should().Be(AiBridgeErrorCode.ReadOnlyServer);
        _graph.Notifier.Requested.Should().BeEmpty();
        await _runnerOpener.DidNotReceiveWithAnyArgs().OpenAsync(default, default);
    }

    [TestMethod]
    public async Task Run_ServerWithoutAiAccess_FailsBeforeAskingTheUser()
    {
        var server = await _graph.AddServerWithoutAiAccessAsync("private", TestContext.CancellationToken);

        var request = new AiCommandRequest(server.Id, "uptime", null);
        var executed = await _service.RunAsync(request, TestContext.CancellationToken);

        executed.Error!.Code.Should().Be(AiBridgeErrorCode.AiAccessDisabled);
        _graph.Notifier.Requested.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Run_ApprovalsOff_RunsWithoutAsking()
    {
        var server = await _graph.AddServerAsync("prod-web", TestContext.CancellationToken);
        _graph.DisableApprovals();
        _graph.Docker.Runner.Respond("uptime", 0, "up");

        var request = new AiCommandRequest(server.Id, "uptime", null);
        var executed = await _service.RunAsync(request, TestContext.CancellationToken);

        executed.Value.StandardOutput.Should().Be("up");
        _graph.Notifier.Requested.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("   ")]
    public async Task Run_EmptyCommand_IsInvalid(string? command)
    {
        var server = await _graph.AddServerAsync("prod-web", TestContext.CancellationToken);

        var request = new AiCommandRequest(server.Id, command, null);
        var executed = await _service.RunAsync(request, TestContext.CancellationToken);

        executed.Error!.Code.Should().Be(AiBridgeErrorCode.InvalidRequest);
    }

    [TestMethod]
    [DataRow(null, 60)]
    [DataRow(0, 1)]
    [DataRow(10, 10)]
    [DataRow(9000, 300)]
    public void TimeoutOf_ClampsToSupportedRange(int? requested, int expectedSeconds)
    {
        AiCommandLimits.TimeoutOf(requested).Should().Be(TimeSpan.FromSeconds(expectedSeconds));
    }
}
