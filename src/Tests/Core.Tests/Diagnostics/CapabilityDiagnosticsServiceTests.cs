using DTech.Core.Connections;
using DTech.Core.Diagnostics;
using DTech.Core.SavedConnections;
using DTech.Core.Tests.Diagnostics.Support;
using DTech.Core.Tests.SavedConnections.Support;
using FluentAssertions;
using NSubstitute;

namespace DTech.Core.Tests.Diagnostics;

[TestClass]
public sealed class CapabilityDiagnosticsServiceTests
{
    private InMemorySavedConnectionRepository _repository = null!;
    private InMemoryCredentialStore _credentials = null!;
    private SavedConnectionService _connections = null!;
    private IRemoteCommandRunnerFactory _runnerFactory = null!;
    private ICapabilityProbe _probe = null!;
    private CapabilityReportCache _cache = null!;
    private ScriptedCommandRunner _runner = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Initialize()
    {
        _repository = new InMemorySavedConnectionRepository();
        _credentials = new InMemoryCredentialStore();
        _connections = new SavedConnectionService(_repository, new ConnectionPasswordStore(_credentials), TimeProvider.System);
        _runner = new ScriptedCommandRunner();
        _runnerFactory = Substitute.For<IRemoteCommandRunnerFactory>();
        _runnerFactory.ConnectAsync(default!, default).ReturnsForAnyArgs(_ => SshResult<IRemoteCommandRunner>.Success(_runner));
        _probe = Substitute.For<ICapabilityProbe>();
        var available = CapabilityResult.Available("probe", "Probe", "fine");
        _probe.ProbeAsync(default!, default).ReturnsForAnyArgs(Task.FromResult<IReadOnlyList<CapabilityResult>>([available]));
        _cache = new CapabilityReportCache();
    }

    [TestMethod]
    public async Task Get_CollectsProbesAndDisposesRunner()
    {
        var connection = await CreateConnectionAsync();

        var result = await CreateService().GetAsync(connection.Id, TestContext.CancellationToken);

        result.Value.State.Should().Be(CapabilityReportState.Completed);
        result.Value.Capabilities.Should().ContainSingle().Which.Id.Should().Be("probe");
        _runner.Disposed.Should().BeTrue();
    }

    [TestMethod]
    public async Task Get_SecondCall_ReturnsCachedReportWithoutReconnecting()
    {
        var connection = await CreateConnectionAsync();
        var service = CreateService();

        var first = await service.GetAsync(connection.Id, TestContext.CancellationToken);
        var second = await service.GetAsync(connection.Id, TestContext.CancellationToken);

        second.Value.Should().BeSameAs(first.Value);
        await _runnerFactory.ReceivedWithAnyArgs(1).ConnectAsync(default!, default);
    }

    [TestMethod]
    public async Task Refresh_BypassesCacheAndReplacesIt()
    {
        var connection = await CreateConnectionAsync();
        var service = CreateService();
        var first = await service.GetAsync(connection.Id, TestContext.CancellationToken);

        var refreshed = await service.RefreshAsync(connection.Id, TestContext.CancellationToken);
        var afterRefresh = await service.GetAsync(connection.Id, TestContext.CancellationToken);

        refreshed.Value.Should().NotBeSameAs(first.Value);
        afterRefresh.Value.Should().BeSameAs(refreshed.Value);
        await _runnerFactory.ReceivedWithAnyArgs(2).ConnectAsync(default!, default);
    }

    [TestMethod]
    public async Task Get_WhenConnectionFails_ReportsFailureAndDoesNotCache()
    {
        var connection = await CreateConnectionAsync();
        var error = new SshError(SshErrorKind.ConnectionFailed, "no route to host");
        _runnerFactory.ConnectAsync(default!, default).ReturnsForAnyArgs(SshResult<IRemoteCommandRunner>.Failure(error));
        var service = CreateService();

        var result = await service.GetAsync(connection.Id, TestContext.CancellationToken);
        await service.GetAsync(connection.Id, TestContext.CancellationToken);

        result.Value.State.Should().Be(CapabilityReportState.ConnectionFailed);
        result.Value.Message.Should().Contain("no route to host");
        result.Value.Capabilities.Should().BeEmpty();
        await _runnerFactory.ReceivedWithAnyArgs(2).ConnectAsync(default!, default);
    }

    [TestMethod]
    public async Task Get_WhenPasswordMissing_ReportsCredentialsUnavailableWithoutConnecting()
    {
        var draft = Draft() with { AuthMethod = ConnectionAuthMethod.Password };
        var connection = await CreateConnectionAsync(draft);

        var result = await CreateService().GetAsync(connection.Id, TestContext.CancellationToken);

        result.Value.State.Should().Be(CapabilityReportState.CredentialsUnavailable);
        await _runnerFactory.DidNotReceiveWithAnyArgs().ConnectAsync(default!, default);
    }

    [TestMethod]
    public async Task Get_OfUnknownConnection_ReturnsNotFound()
    {
        var result = await CreateService().GetAsync(Guid.NewGuid(), TestContext.CancellationToken);

        result.Error!.Code.Should().Be(SavedConnectionErrorCode.NotFound);
    }

    [TestMethod]
    public async Task Get_WhenProbeThrows_ReportsUnknownAndKeepsOtherProbes()
    {
        var connection = await CreateConnectionAsync();
        var broken = Substitute.For<ICapabilityProbe>();
        broken.ProbeAsync(default!, default).ReturnsForAnyArgs<Task<IReadOnlyList<CapabilityResult>>>(_ => throw new InvalidOperationException("parser blew up"));

        var result = await CreateService(broken, _probe).GetAsync(connection.Id, TestContext.CancellationToken);

        result.Value.Capabilities.Should().HaveCount(2);
        result.Value.Capabilities[0].Status.Should().Be(CapabilityStatus.Unknown);
        result.Value.Capabilities[1].Id.Should().Be("probe");
    }

    [TestMethod]
    public async Task Get_WhenCallerCancels_Throws()
    {
        var connection = await CreateConnectionAsync();
        var cancelling = Substitute.For<ICapabilityProbe>();
        cancelling.ProbeAsync(default!, default).ReturnsForAnyArgs<Task<IReadOnlyList<CapabilityResult>>>(_ => throw new OperationCanceledException());

        var act = () => CreateService(cancelling).GetAsync(connection.Id, TestContext.CancellationToken);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static SavedConnectionDraft Draft()
    {
        return new SavedConnectionDraft { Name = "web", Host = "example.org", UserName = "deploy" };
    }

    private CapabilityDiagnosticsService CreateService(params ICapabilityProbe[] probes)
    {
        var targetFactory = new ConnectionTargetFactory(new ConnectionPasswordStore(_credentials));
        var activeProbes = probes.Length == 0 ? [_probe] : probes;
        return new CapabilityDiagnosticsService(_repository, targetFactory, _runnerFactory, activeProbes, _cache, TimeProvider.System);
    }

    private async Task<SavedConnection> CreateConnectionAsync(SavedConnectionDraft? draft = null)
    {
        var submission = new SavedConnectionSubmission(draft ?? Draft(), null);
        var result = await _connections.CreateAsync(submission, TestContext.CancellationToken);
        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        return result.Value;
    }
}
