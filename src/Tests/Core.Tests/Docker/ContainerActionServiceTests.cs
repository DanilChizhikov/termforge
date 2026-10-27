using DTech.Core.Docker;
using DTech.Core.Operations;
using DTech.Core.Safety;
using DTech.Core.Tests.Docker.Support;
using FluentAssertions;

namespace DTech.Core.Tests.Docker;

[TestClass]
public sealed class ContainerActionServiceTests
{
    private DockerTestGraph _graph = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Initialize()
    {
        _graph = new DockerTestGraph();
    }

    [TestMethod]
    public async Task Start_WithoutConfirmation_RunsQuotedCommandAndRecordsSuccess()
    {
        var connection = await _graph.AddConnectionAsync("web", TestContext.CancellationToken);
        _graph.Runner.Respond("docker start", 0, "db\n");
        var service = _graph.CreateActionService();
        var request = new ContainerActionRequest(connection.Id, "db", DockerContainerAction.Start);

        var result = await service.RunAsync(request, TestContext.CancellationToken);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        _graph.Runner.ExecutedCommands.Should().ContainSingle().Which.Should().Be("docker start 'db'");
        var operations = await _graph.ListOperationsAsync(TestContext.CancellationToken);
        var operation = operations.Should().ContainSingle().Subject;
        operation.Id.Should().Be(result.Value.OperationId);
        operation.Kind.Should().Be(DockerOperationRecorder.StartKind);
        operation.Server.Should().Be("web");
        operation.Status.Should().Be(OperationStatus.Succeeded);
        operation.Details.Should().Contain("container", "db");
        operation.Retryable.Should().BeFalse();
    }

    [TestMethod]
    [DataRow(DockerContainerAction.Stop)]
    [DataRow(DockerContainerAction.Restart)]
    public async Task StopOrRestart_WithoutConfirmation_IsRejectedBeforeAnyCommand(DockerContainerAction action)
    {
        var connection = await _graph.AddConnectionAsync("web", TestContext.CancellationToken);
        var service = _graph.CreateActionService();
        var request = new ContainerActionRequest(connection.Id, "web", action);

        var result = await service.RunAsync(request, TestContext.CancellationToken);

        result.Error!.Code.Should().Be(DockerErrorCode.ConfirmationRequired);
        _graph.RunnerFactory.Connections.Should().Be(0);
        var operations = await _graph.ListOperationsAsync(TestContext.CancellationToken);
        operations.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Stop_WithConfirmation_RunsAndRecordsStopKind()
    {
        var connection = await _graph.AddConnectionAsync("web", TestContext.CancellationToken);
        _graph.Runner.Respond("docker stop", 0, "web\n");
        var service = _graph.CreateActionService();
        var token = _graph.Safety.Confirm(DangerousOperation.ContainerStop, connection.Id, "web");
        var request = new ContainerActionRequest(connection.Id, "web", DockerContainerAction.Stop) { ConfirmationToken = token };

        var result = await service.RunAsync(request, TestContext.CancellationToken);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        _graph.Runner.ExecutedCommands.Should().ContainSingle().Which.Should().Be("docker stop 'web'");
        var operations = await _graph.ListOperationsAsync(TestContext.CancellationToken);
        operations.Single().Kind.Should().Be(DockerOperationRecorder.StopKind);
    }

    [TestMethod]
    public async Task Stop_WithTokenIssuedForRestart_IsRejected()
    {
        var connection = await _graph.AddConnectionAsync("web", TestContext.CancellationToken);
        var service = _graph.CreateActionService();
        var token = _graph.Safety.Confirm(DangerousOperation.ContainerRestart, connection.Id, "web");
        var request = new ContainerActionRequest(connection.Id, "web", DockerContainerAction.Stop) { ConfirmationToken = token };

        var result = await service.RunAsync(request, TestContext.CancellationToken);

        result.Error!.Code.Should().Be(DockerErrorCode.ConfirmationRequired);
        _graph.RunnerFactory.Connections.Should().Be(0);
    }

    [TestMethod]
    [DataRow(DockerContainerAction.Start)]
    [DataRow(DockerContainerAction.Stop)]
    [DataRow(DockerContainerAction.Restart)]
    public async Task Action_OnReadOnlyServer_IsBlockedBeforeAnyCommand(DockerContainerAction action)
    {
        var connection = await _graph.AddConnectionAsync("web", TestContext.CancellationToken);
        _graph.Safety.MarkReadOnly(connection.Id);
        var service = _graph.CreateActionService();
        var request = new ContainerActionRequest(connection.Id, "web", action);

        var result = await service.RunAsync(request, TestContext.CancellationToken);

        result.Error!.Code.Should().Be(DockerErrorCode.ReadOnlyServer);
        _graph.RunnerFactory.Connections.Should().Be(0);
        var operations = await _graph.ListOperationsAsync(TestContext.CancellationToken);
        operations.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Restart_WhenDockerFails_RecordsFailedOperationWithReason()
    {
        var connection = await _graph.AddConnectionAsync("web", TestContext.CancellationToken);
        _graph.Runner.Respond("docker restart", 1, string.Empty, DockerRecordedOutputs.PermissionDenied);
        var service = _graph.CreateActionService();
        var token = _graph.Safety.Confirm(DangerousOperation.ContainerRestart, connection.Id, "web");
        var request = new ContainerActionRequest(connection.Id, "web", DockerContainerAction.Restart) { ConfirmationToken = token };

        var result = await service.RunAsync(request, TestContext.CancellationToken);

        result.Error!.Code.Should().Be(DockerErrorCode.PermissionDenied);
        var operations = await _graph.ListOperationsAsync(TestContext.CancellationToken);
        var operation = operations.Should().ContainSingle().Subject;
        operation.Kind.Should().Be(DockerOperationRecorder.RestartKind);
        operation.Status.Should().Be(OperationStatus.Failed);
        operation.Error.Should().Contain("docker group");
    }

    [TestMethod]
    public async Task Start_WhenContainerIsGone_ReturnsContainerNotFound()
    {
        var connection = await _graph.AddConnectionAsync("web", TestContext.CancellationToken);
        _graph.Runner.Respond("docker start", 1, string.Empty, DockerRecordedOutputs.NoSuchContainer);
        var service = _graph.CreateActionService();
        var request = new ContainerActionRequest(connection.Id, "ghost", DockerContainerAction.Start);

        var result = await service.RunAsync(request, TestContext.CancellationToken);

        result.Error!.Code.Should().Be(DockerErrorCode.ContainerNotFound);
    }

    [TestMethod]
    [DataRow("web;reboot")]
    [DataRow("$(reboot)")]
    [DataRow("web' && reboot '")]
    public async Task Action_WithUnsafeContainer_IsRejectedWithoutConnecting(string container)
    {
        var connection = await _graph.AddConnectionAsync("web", TestContext.CancellationToken);
        var service = _graph.CreateActionService();
        var token = _graph.Safety.Confirm(DangerousOperation.ContainerRestart, connection.Id, container);
        var request = new ContainerActionRequest(connection.Id, container, DockerContainerAction.Restart) { ConfirmationToken = token };

        var result = await service.RunAsync(request, TestContext.CancellationToken);

        result.Error!.Code.Should().Be(DockerErrorCode.InvalidRequest);
        _graph.RunnerFactory.Connections.Should().Be(0);
        _graph.Runner.ExecutedCommands.Should().BeEmpty();
    }
}
