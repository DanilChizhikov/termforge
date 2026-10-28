using DTech.Core.Docker;
using DTech.Core.Operations;
using DTech.Core.Safety;
using DTech.Core.Tests.Docker.Support;
using FluentAssertions;

namespace DTech.Core.Tests.Docker;

[TestClass]
public sealed class ComposeActionServiceTests
{
    private const string ShopContainers =
        "{\"ID\":\"3f4e1b2c9d8a\",\"Image\":\"nginx\",\"Names\":\"shop-web-1\",\"State\":\"running\",\"Status\":\"Up 1 hour\","
        + "\"Labels\":\"com.docker.compose.project=shop,com.docker.compose.project.config_files=/srv/shop/compose.yml,com.docker.compose.project.working_dir=/srv/shop\"}\n";

    private DockerTestGraph _graph = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Initialize()
    {
        _graph = new DockerTestGraph();
    }

    [TestMethod]
    [DataRow(ComposeAction.Restart)]
    [DataRow(ComposeAction.Recreate)]
    public async Task Action_WithoutConfirmation_IsRejectedBeforeAnyCommand(ComposeAction action)
    {
        var connection = await _graph.AddConnectionAsync("web", TestContext.CancellationToken);
        var service = _graph.CreateComposeService();
        var request = new ComposeActionRequest(connection.Id, "shop", action);

        var result = await service.RunAsync(request, TestContext.CancellationToken);

        result.Error!.Code.Should().Be(DockerErrorCode.ConfirmationRequired);
        _graph.RunnerFactory.Connections.Should().Be(0);
    }

    [TestMethod]
    public async Task Restart_WithConfirmation_RestartsProjectAndRecordsKind()
    {
        var connection = await _graph.AddConnectionAsync("web", TestContext.CancellationToken);
        _graph.Runner.Respond("docker ps", 0, ShopContainers).Respond("docker compose", 0);
        var service = _graph.CreateComposeService();
        var token = _graph.Safety.Confirm(DangerousOperation.ComposeRestart, connection.Id, "shop");
        var request = new ComposeActionRequest(connection.Id, "shop", ComposeAction.Restart) { ConfirmationToken = token };

        var result = await service.RunAsync(request, TestContext.CancellationToken);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        _graph.Runner.ExecutedCommands.Should().HaveCount(2);
        _graph.Runner.ExecutedCommands[1].Should().Be("docker compose -p 'shop' restart");
        var operation = (await _graph.ListOperationsAsync(TestContext.CancellationToken)).Single();
        operation.Kind.Should().Be(DockerOperationRecorder.ComposeRestartKind);
        operation.Status.Should().Be(OperationStatus.Succeeded);
        operation.Details.Should().Contain("project", "shop");
    }

    [TestMethod]
    public async Task Recreate_UsesFilesFromServerLabels()
    {
        var connection = await _graph.AddConnectionAsync("web", TestContext.CancellationToken);
        _graph.Runner.Respond("docker ps", 0, ShopContainers).Respond("docker compose", 0);
        var service = _graph.CreateComposeService();
        var token = _graph.Safety.Confirm(DangerousOperation.ComposeRecreate, connection.Id, "shop");
        var request = new ComposeActionRequest(connection.Id, "shop", ComposeAction.Recreate) { ConfirmationToken = token };

        var result = await service.RunAsync(request, TestContext.CancellationToken);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        const string compose = "docker compose -p 'shop' --project-directory '/srv/shop' -f '/srv/shop/compose.yml'";
        _graph.Runner.ExecutedCommands[1].Should().Be($"{compose} down && {compose} up -d");
    }

    [TestMethod]
    public async Task Restart_UnknownProject_FailsAndRecordsFailure()
    {
        var connection = await _graph.AddConnectionAsync("web", TestContext.CancellationToken);
        _graph.Runner.Respond("docker ps", 0, string.Empty);
        var service = _graph.CreateComposeService();
        var token = _graph.Safety.Confirm(DangerousOperation.ComposeRestart, connection.Id, "ghost");
        var request = new ComposeActionRequest(connection.Id, "ghost", ComposeAction.Restart) { ConfirmationToken = token };

        var result = await service.RunAsync(request, TestContext.CancellationToken);

        result.Error!.Code.Should().Be(DockerErrorCode.ComposeProjectNotFound);
        _graph.Runner.ExecutedCommands.Should().ContainSingle();
        var operation = (await _graph.ListOperationsAsync(TestContext.CancellationToken)).Single();
        operation.Status.Should().Be(OperationStatus.Failed);
    }

    [TestMethod]
    public async Task Action_WithInvalidProject_IsInvalidRequest()
    {
        var connection = await _graph.AddConnectionAsync("web", TestContext.CancellationToken);
        var service = _graph.CreateComposeService();
        var request = new ComposeActionRequest(connection.Id, "shop;reboot", ComposeAction.Restart);

        var result = await service.RunAsync(request, TestContext.CancellationToken);

        result.Error!.Code.Should().Be(DockerErrorCode.InvalidRequest);
    }

    [TestMethod]
    public async Task Action_OnReadOnlyServer_IsBlocked()
    {
        var connection = await _graph.AddConnectionAsync("web", TestContext.CancellationToken);
        _graph.Safety.MarkReadOnly(connection.Id);
        var service = _graph.CreateComposeService();
        var request = new ComposeActionRequest(connection.Id, "shop", ComposeAction.Restart);

        var result = await service.RunAsync(request, TestContext.CancellationToken);

        result.Error!.Code.Should().Be(DockerErrorCode.ReadOnlyServer);
    }
}
