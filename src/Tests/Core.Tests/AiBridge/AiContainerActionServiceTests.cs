using DTech.Core.AiBridge;
using DTech.Core.AiBridge.Approvals;
using DTech.Core.AiBridge.Docker;
using DTech.Core.Docker;
using DTech.Core.Tests.AiBridge.Support;
using FluentAssertions;

namespace DTech.Core.Tests.AiBridge;

[TestClass]
public sealed class AiContainerActionServiceTests
{
    private AiBridgeTestGraph _graph = null!;
    private AiContainerActionService _service = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Initialize()
    {
        _graph = new AiBridgeTestGraph();
        var actionService = _graph.Docker.CreateActionService();
        _service = new AiContainerActionService(_graph.AccessPolicy, _graph.Gate, actionService, _graph.Docker.Safety.Confirmations);
    }

    [TestMethod]
    [DataRow(DockerContainerAction.Start, "docker start 'web'")]
    [DataRow(DockerContainerAction.Stop, "docker stop 'web'")]
    [DataRow(DockerContainerAction.Restart, "docker restart 'web'")]
    [DataRow(DockerContainerAction.Remove, "docker rm -f 'web'")]
    public async Task Run_Approved_RunsTheActionWithoutASecondConfirmation(DockerContainerAction action, string expectedCommand)
    {
        var server = await _graph.AddServerAsync("prod-web", TestContext.CancellationToken);
        _graph.Docker.Runner.Respond("docker", 0);

        var request = new AiContainerActionRequest(server.Id, "web", action);
        var completed = await _service.RunAsync(request, TestContext.CancellationToken);

        completed.Value.Container.Should().Be("web");
        _graph.Docker.Runner.ExecutedCommands.Should().Equal(expectedCommand);
        _graph.Notifier.Requested.Should().ContainSingle().Which.Kind.Should().Be(AiActionKind.ContainerAction);
    }

    [TestMethod]
    public async Task Run_Denied_DoesNotTouchTheContainer()
    {
        var server = await _graph.AddServerAsync("prod-web", TestContext.CancellationToken);
        _graph.Notifier.Answer = AiApprovalOutcome.Denied;

        var request = new AiContainerActionRequest(server.Id, "web", DockerContainerAction.Stop);
        var completed = await _service.RunAsync(request, TestContext.CancellationToken);

        completed.Error!.Code.Should().Be(AiBridgeErrorCode.Denied);
        _graph.Docker.RunnerFactory.Connections.Should().Be(0);
    }

    [TestMethod]
    public async Task Run_ReadOnlyServer_IsBlocked()
    {
        var server = await _graph.AddReadOnlyServerAsync("prod-db", TestContext.CancellationToken);

        var request = new AiContainerActionRequest(server.Id, "db", DockerContainerAction.Restart);
        var completed = await _service.RunAsync(request, TestContext.CancellationToken);

        completed.Error!.Code.Should().Be(AiBridgeErrorCode.ReadOnlyServer);
        _graph.Notifier.Requested.Should().BeEmpty();
        _graph.Docker.RunnerFactory.Connections.Should().Be(0);
    }

    [TestMethod]
    public async Task Run_InvalidContainer_IsRejected()
    {
        var server = await _graph.AddServerAsync("prod-web", TestContext.CancellationToken);

        var request = new AiContainerActionRequest(server.Id, "web; rm -rf /", DockerContainerAction.Stop);
        var completed = await _service.RunAsync(request, TestContext.CancellationToken);

        completed.Error!.Code.Should().Be(AiBridgeErrorCode.InvalidRequest);
        _graph.Notifier.Requested.Should().BeEmpty();
    }
}
