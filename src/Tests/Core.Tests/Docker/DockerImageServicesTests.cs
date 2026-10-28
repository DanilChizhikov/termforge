using DTech.Core.Connections;
using DTech.Core.Diagnostics;
using DTech.Core.Docker;
using DTech.Core.Operations;
using DTech.Core.Safety;
using DTech.Core.Tests.Docker.Support;
using FluentAssertions;
using NSubstitute;

namespace DTech.Core.Tests.Docker;

[TestClass]
public sealed class DockerImageServicesTests
{
    private DockerTestGraph _graph = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Initialize()
    {
        _graph = new DockerTestGraph();
    }

    [TestMethod]
    public async Task List_ReturnsParsedImagesWithServerName()
    {
        var connection = await _graph.AddConnectionAsync("web", TestContext.CancellationToken);
        _graph.Runner.Respond("docker image ls", 0, "{\"ID\":\"sha256:3f4e\",\"Repository\":\"nginx\",\"Tag\":\"latest\",\"Size\":\"40MB\"}\n");
        var service = _graph.CreateImageService();

        var result = await service.ListAsync(connection.Id, TestContext.CancellationToken);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.ServerName.Should().Be("web");
        result.Value.Images.Should().ContainSingle().Which.Repository.Should().Be("nginx");
    }

    [TestMethod]
    public async Task Remove_WithoutConfirmation_IsRejectedBeforeAnyCommand()
    {
        var connection = await _graph.AddConnectionAsync("web", TestContext.CancellationToken);
        var service = _graph.CreateImageRemoveService();
        var request = new ImageRemoveRequest(connection.Id, "nginx:latest");

        var result = await service.RemoveAsync(request, TestContext.CancellationToken);

        result.Error!.Code.Should().Be(DockerErrorCode.ConfirmationRequired);
        _graph.RunnerFactory.Connections.Should().Be(0);
    }

    [TestMethod]
    public async Task Remove_WithConfirmation_RunsRmiAndRecordsKind()
    {
        var connection = await _graph.AddConnectionAsync("web", TestContext.CancellationToken);
        _graph.Runner.Respond("docker rmi", 0, "Untagged: nginx:latest\n");
        var service = _graph.CreateImageRemoveService();
        var token = _graph.Safety.Confirm(DangerousOperation.ImageRemove, connection.Id, "nginx:latest");
        var request = new ImageRemoveRequest(connection.Id, "nginx:latest") { ConfirmationToken = token };

        var result = await service.RemoveAsync(request, TestContext.CancellationToken);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        _graph.Runner.ExecutedCommands.Should().ContainSingle().Which.Should().Be("docker rmi 'nginx:latest'");
        var operation = (await _graph.ListOperationsAsync(TestContext.CancellationToken)).Single();
        operation.Kind.Should().Be(DockerOperationRecorder.ImageRemoveKind);
        operation.Details.Should().Contain("image", "nginx:latest");
    }

    [TestMethod]
    public async Task Remove_WhenImageInUse_ReportsImageInUseAndRecordsFailure()
    {
        var connection = await _graph.AddConnectionAsync("web", TestContext.CancellationToken);
        _graph.Runner.Respond("docker rmi", 1, string.Empty, "Error response from daemon: conflict: unable to remove repository reference \"nginx\" (must force)");
        var service = _graph.CreateImageRemoveService();
        var token = _graph.Safety.Confirm(DangerousOperation.ImageRemove, connection.Id, "nginx");
        var request = new ImageRemoveRequest(connection.Id, "nginx") { ConfirmationToken = token };

        var result = await service.RemoveAsync(request, TestContext.CancellationToken);

        result.Error!.Code.Should().Be(DockerErrorCode.ImageInUse);
        var operation = (await _graph.ListOperationsAsync(TestContext.CancellationToken)).Single();
        operation.Status.Should().Be(OperationStatus.Failed);
    }

    [TestMethod]
    public async Task Pull_StreamsAndRecordsSuccessWhenDockerExitsZero()
    {
        var connection = await _graph.AddConnectionAsync("web", TestContext.CancellationToken);
        var output = "latest: Pulling from library/alpine\n"u8.ToArray();
        var recorded = new RecordedCommandStream([output], 0);
        var started = SshResult<IRemoteCommandStream>.Success(recorded);
        _graph.StreamFactory.StartAsync(default!, default!, default).ReturnsForAnyArgs(started);
        var service = _graph.CreatePullService();
        var request = new ImagePullRequest(connection.Id, "alpine:3.20");

        var opened = await service.OpenAsync(request, TestContext.CancellationToken);
        opened.IsSuccess.Should().BeTrue(opened.Error?.Message);
        await using (var stream = opened.Value)
        {
            var text = await stream.ReadAsync(TestContext.CancellationToken);
            text.Should().Contain("Pulling from");
            (await stream.ReadAsync(TestContext.CancellationToken)).Should().BeNull();
        }

        await _graph.StreamFactory.Received(1).StartAsync(Arg.Any<SshConnectionTarget>(), "docker pull 'alpine:3.20' 2>&1", Arg.Any<CancellationToken>());
        recorded.Disposed.Should().BeTrue();
        var operation = (await _graph.ListOperationsAsync(TestContext.CancellationToken)).Single();
        operation.Kind.Should().Be(DockerOperationRecorder.PullKind);
        operation.Status.Should().Be(OperationStatus.Succeeded);
    }

    [TestMethod]
    public async Task Pull_WhenDockerFails_RecordsFailure()
    {
        var connection = await _graph.AddConnectionAsync("web", TestContext.CancellationToken);
        var recorded = new RecordedCommandStream([], 1);
        var started = SshResult<IRemoteCommandStream>.Success(recorded);
        _graph.StreamFactory.StartAsync(default!, default!, default).ReturnsForAnyArgs(started);
        var service = _graph.CreatePullService();
        var request = new ImagePullRequest(connection.Id, "missing/image");

        var opened = await service.OpenAsync(request, TestContext.CancellationToken);
        await using (var stream = opened.Value)
        {
            (await stream.ReadAsync(TestContext.CancellationToken)).Should().BeNull();
        }

        var operation = (await _graph.ListOperationsAsync(TestContext.CancellationToken)).Single();
        operation.Status.Should().Be(OperationStatus.Failed);
    }

    [TestMethod]
    public async Task Pull_OnReadOnlyServer_IsBlockedWithoutStarting()
    {
        var connection = await _graph.AddConnectionAsync("web", TestContext.CancellationToken);
        _graph.Safety.MarkReadOnly(connection.Id);
        var service = _graph.CreatePullService();
        var request = new ImagePullRequest(connection.Id, "alpine");

        var opened = await service.OpenAsync(request, TestContext.CancellationToken);

        opened.Error!.Code.Should().Be(DockerErrorCode.ReadOnlyServer);
        await _graph.StreamFactory.DidNotReceiveWithAnyArgs().StartAsync(default!, default!, default);
    }

    [TestMethod]
    public async Task Pull_WithUnsafeReference_IsInvalidRequest()
    {
        var connection = await _graph.AddConnectionAsync("web", TestContext.CancellationToken);
        var service = _graph.CreatePullService();
        var request = new ImagePullRequest(connection.Id, "alpine;reboot");

        var opened = await service.OpenAsync(request, TestContext.CancellationToken);

        opened.Error!.Code.Should().Be(DockerErrorCode.InvalidRequest);
        await _graph.StreamFactory.DidNotReceiveWithAnyArgs().StartAsync(default!, default!, default);
    }
}
