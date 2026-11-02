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
    [DataRow(ComposeAction.Recreate, false, true)]
    [DataRow(ComposeAction.Recreate, true, false)]
    [DataRow(ComposeAction.Restart, false, false)]
    [DataRow(ComposeAction.Restart, true, true)]
    public async Task Action_UsesExpectedCancellationAndRecordsOutcome(ComposeAction action, bool stopApplication, bool succeeds)
    {
        using var requestCancellation = new CancellationTokenSource();
        using var applicationStopping = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runner = Substitute.For<IRemoteCommandRunner>();
        runner.RunAsync(Arg.Any<RemoteCommandRequest>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            var command = call.ArgAt<RemoteCommandRequest>(0);
            var executionToken = call.ArgAt<CancellationToken>(1);
            if (command.Command.StartsWith("docker ps", StringComparison.Ordinal))
            {
                return SshResult<RemoteCommandResult>.Success(new RemoteCommandResult(0, ShopContainers, string.Empty));
            }

            started.SetResult();
            try
            {
                await finish.Task.WaitAsync(executionToken);
                return SshResult<RemoteCommandResult>.Success(new RemoteCommandResult(0, string.Empty, string.Empty));
            }
            catch (OperationCanceledException)
            {
                return SshResult<RemoteCommandResult>.Failure(new SshError(SshErrorKind.Cancelled, "Command cancelled"));
            }
        });
        var factory = Substitute.For<IRemoteCommandRunnerFactory>();
        factory.ConnectAsync(Arg.Any<SshConnectionTarget>(), Arg.Any<CancellationToken>())
            .Returns(SshResult<IRemoteCommandRunner>.Success(runner));
        var service = new ComposeActionService(_graph.TargetResolver, factory,
            new DockerOperationRecorder(_graph.Journal), _graph.Safety.Gate, new ComposeProjectResolver());
        var connection = await _graph.AddConnectionAsync("web", TestContext.CancellationToken);
        var operation = action == ComposeAction.Recreate ? DangerousOperation.ComposeRecreate : DangerousOperation.ComposeRestart;
        var confirmation = _graph.Safety.Confirm(operation, connection.Id, "shop");
        var request = new ComposeActionRequest(connection.Id, "shop", action) { ConfirmationToken = confirmation };

        var running = service.RunAsync(request, requestCancellation.Token, applicationStopping.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);
        await (stopApplication ? applicationStopping : requestCancellation).CancelAsync();
        finish.SetResult();
        var result = await running.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);

        result.IsSuccess.Should().Be(succeeds);
        var journal = (await _graph.ListOperationsAsync(TestContext.CancellationToken)).Single();
        journal.Status.Should().Be(succeeds ? OperationStatus.Succeeded : OperationStatus.Failed);
        await runner.Received(1).DisposeAsync();
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
