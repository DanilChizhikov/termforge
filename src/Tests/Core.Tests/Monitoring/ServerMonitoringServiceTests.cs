using DTech.Core.Connections;
using DTech.Core.Diagnostics;
using DTech.Core.Monitoring;
using DTech.Core.SavedConnections;
using DTech.Core.Tests.Diagnostics.Support;
using DTech.Core.Tests.Monitoring.Support;
using DTech.Core.Tests.SavedConnections.Support;
using FluentAssertions;
using NSubstitute;

namespace DTech.Core.Tests.Monitoring;

[TestClass]
public sealed class ServerMonitoringServiceTests
{
    private InMemorySavedConnectionRepository _repository = null!;
    private InMemoryCredentialStore _credentials = null!;
    private SavedConnectionService _connections = null!;
    private IRemoteCommandRunnerFactory _runnerFactory = null!;
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
    }

    [TestMethod]
    public async Task Collect_ReturnsParsedReadingsAndDisposesRunner()
    {
        var connection = await CreateConnectionAsync();
        _runner.Respond("uname -s", 0, RecordedMetricOutputs.GlibcLinux);

        var result = await CreateService().CollectAsync(connection.Id, TestContext.CancellationToken);

        result.Value.State.Should().Be(MonitoringState.Connected);
        result.Value.Readings.CpuPercent.Value.Should().Be(48.5);
        _runner.Disposed.Should().BeTrue();
    }

    [TestMethod]
    public async Task Collect_WhenConnectionFails_ReportsStateAndNoValues()
    {
        var connection = await CreateConnectionAsync();
        var error = new SshError(SshErrorKind.ConnectionFailed, "no route to host");
        _runnerFactory.ConnectAsync(default!, default).ReturnsForAnyArgs(SshResult<IRemoteCommandRunner>.Failure(error));

        var result = await CreateService().CollectAsync(connection.Id, TestContext.CancellationToken);

        result.Value.State.Should().Be(MonitoringState.ConnectionFailed);
        result.Value.Message.Should().Contain("no route to host");
        result.Value.Readings.Memory.IsAvailable.Should().BeFalse();
        result.Value.Readings.Memory.Reason.Should().NotBeNullOrWhiteSpace();
    }

    [TestMethod]
    public async Task Collect_WhenCommandFails_ReportsConnectionFailed()
    {
        var connection = await CreateConnectionAsync();
        _runner.Fail("uname -s", SshErrorKind.ConnectionFailed, "connection reset");

        var result = await CreateService().CollectAsync(connection.Id, TestContext.CancellationToken);

        result.Value.State.Should().Be(MonitoringState.ConnectionFailed);
        result.Value.Message.Should().Contain("connection reset");
    }

    [TestMethod]
    public async Task Collect_WhenPasswordMissing_DoesNotConnect()
    {
        var draft = Draft() with { AuthMethod = ConnectionAuthMethod.Password };
        var connection = await CreateConnectionAsync(draft);

        var result = await CreateService().CollectAsync(connection.Id, TestContext.CancellationToken);

        result.Value.State.Should().Be(MonitoringState.CredentialsUnavailable);
        await _runnerFactory.DidNotReceiveWithAnyArgs().ConnectAsync(default!, default);
    }

    [TestMethod]
    public async Task Collect_OfUnknownConnection_ReturnsNotFound()
    {
        var result = await CreateService().CollectAsync(Guid.NewGuid(), TestContext.CancellationToken);

        result.Error!.Code.Should().Be(SavedConnectionErrorCode.NotFound);
    }

    private static SavedConnectionDraft Draft()
    {
        return new SavedConnectionDraft { Name = "web", Host = "example.org", UserName = "deploy" };
    }

    private ServerMonitoringService CreateService()
    {
        var targetFactory = new ConnectionTargetFactory(new ConnectionPasswordStore(_credentials));
        var collector = new ServerMetricsCollector();
        return new ServerMonitoringService(_repository, targetFactory, _runnerFactory, collector, TimeProvider.System);
    }

    private async Task<SavedConnection> CreateConnectionAsync(SavedConnectionDraft? draft = null)
    {
        var submission = new SavedConnectionSubmission(draft ?? Draft(), null);
        var result = await _connections.CreateAsync(submission, TestContext.CancellationToken);
        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        return result.Value;
    }
}
