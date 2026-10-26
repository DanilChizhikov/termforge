using DTech.Core.Connections;
using DTech.Core.Docker;
using DTech.Core.Tests.Diagnostics.Support;
using DTech.Core.Tests.Docker.Support;
using FluentAssertions;

namespace DTech.Core.Tests.Docker;

[TestClass]
public sealed class DockerOverviewServiceTests
{
    private DockerTestGraph _graph = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Initialize()
    {
        _graph = new DockerTestGraph();
    }

    [TestMethod]
    public async Task Get_WhenDockerAccessible_ListsContainersFromJsonOutput()
    {
        var connection = await _graph.AddConnectionAsync("web", TestContext.CancellationToken);
        _graph.Runner
            .Respond("command -v docker", 0, "/usr/bin/docker\n")
            .Respond("docker info", 0, "27.3.1\n")
            .Respond("docker ps", 0, DockerRecordedOutputs.ContainerList);
        var service = _graph.CreateOverviewService();

        var result = await service.GetAsync(connection.Id, TestContext.CancellationToken);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.ServerName.Should().Be("web");
        result.Value.Availability.State.Should().Be(DockerAvailabilityState.Available);
        result.Value.Containers.Select(container => container.Name).Should().Equal("web", "db", "worker.paused");
        _graph.Runner.ExecutedCommands.Should().Contain(command => command.Contains("--format '{{json .}}'", StringComparison.Ordinal));
        _graph.Runner.Disposed.Should().BeTrue();
    }

    [TestMethod]
    public async Task Get_WhenDockerMissing_ReportsNotInstalledWithoutListing()
    {
        var connection = await _graph.AddConnectionAsync("web", TestContext.CancellationToken);
        _graph.Runner.Respond("command -v docker", 127);
        var service = _graph.CreateOverviewService();

        var result = await service.GetAsync(connection.Id, TestContext.CancellationToken);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.Availability.State.Should().Be(DockerAvailabilityState.NotInstalled);
        result.Value.Availability.Reason.Should().Contain("not installed");
        result.Value.Containers.Should().BeEmpty();
        _graph.Runner.ExecutedCommands.Should().NotContain(command => command.Contains("docker ps", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Get_WhenPermissionDenied_ReportsNoAccessWithDockerGroupHint()
    {
        var connection = await _graph.AddConnectionAsync("web", TestContext.CancellationToken);
        _graph.Runner
            .Respond("command -v docker", 0, "/usr/bin/docker\n")
            .Respond("docker info", 1, DockerRecordedOutputs.PermissionDenied);
        var service = _graph.CreateOverviewService();

        var result = await service.GetAsync(connection.Id, TestContext.CancellationToken);

        result.Value.Availability.State.Should().Be(DockerAvailabilityState.NoAccess);
        result.Value.Availability.Reason.Should().Contain("docker group");
        result.Value.Containers.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Get_WhenListingIsDeniedAfterProbe_ReturnsPermissionDenied()
    {
        var connection = await _graph.AddConnectionAsync("web", TestContext.CancellationToken);
        _graph.Runner
            .Respond("command -v docker", 0, "/usr/bin/docker\n")
            .Respond("docker info", 0, "27.3.1\n")
            .Respond("docker ps", 1, string.Empty, DockerRecordedOutputs.PermissionDenied);
        var service = _graph.CreateOverviewService();

        var result = await service.GetAsync(connection.Id, TestContext.CancellationToken);

        result.Error!.Code.Should().Be(DockerErrorCode.PermissionDenied);
    }

    [TestMethod]
    public async Task Get_ForUnknownConnection_ReturnsConnectionNotFound()
    {
        var service = _graph.CreateOverviewService();

        var unknownId = Guid.NewGuid();
        var result = await service.GetAsync(unknownId, TestContext.CancellationToken);

        result.Error!.Code.Should().Be(DockerErrorCode.ConnectionNotFound);
        _graph.RunnerFactory.Connections.Should().Be(0);
    }

    [TestMethod]
    public async Task Get_WhenSshFails_ReturnsConnectionFailedWithKind()
    {
        var connection = await _graph.AddConnectionAsync("web", TestContext.CancellationToken);
        _graph.RunnerFactory.ConnectionError = new SshError(SshErrorKind.HostKeyChanged, "host unreachable");
        var service = _graph.CreateOverviewService();

        var result = await service.GetAsync(connection.Id, TestContext.CancellationToken);

        result.Error!.Code.Should().Be(DockerErrorCode.ConnectionFailed);
        result.Error.SshErrorKind.Should().Be(SshErrorKind.HostKeyChanged);
    }
}
