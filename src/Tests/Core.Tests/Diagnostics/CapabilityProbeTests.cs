using DTech.Core.Connections;
using DTech.Core.Diagnostics;
using DTech.Core.Diagnostics.Probes;
using DTech.Core.Files;
using DTech.Core.Tests.Diagnostics.Support;
using FluentAssertions;
using NSubstitute;

namespace DTech.Core.Tests.Diagnostics;

[TestClass]
public sealed class CapabilityProbeTests
{
    private static readonly SshConnectionTarget Target = new(new SshEndpoint("example.org", 22), "deploy", new AgentAuthentication(null));

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task System_OnAlpine_IsAvailableWithDescription()
    {
        var runner = new ScriptedCommandRunner().Respond("uname", 0, RecordedOutputs.AlpineSystem);

        var results = await new SystemProbe().ProbeAsync(Context(runner), TestContext.CancellationToken);

        results.Should().ContainSingle().Which.Should().Match<CapabilityResult>(result => result.Status == CapabilityStatus.Available && result.Reason.Contains("Alpine Linux v3.22"));
    }

    [TestMethod]
    public async Task System_WithEmptyOutput_IsUnknown()
    {
        var runner = new ScriptedCommandRunner().Respond("uname", 0, RecordedOutputs.MissingEverything);

        var results = await new SystemProbe().ProbeAsync(Context(runner), TestContext.CancellationToken);

        results.Single().Status.Should().Be(CapabilityStatus.Unknown);
    }

    [TestMethod]
    public async Task System_WhenRunnerFails_IsUnknownWithReason()
    {
        var runner = new ScriptedCommandRunner().Fail("uname", SshErrorKind.SessionClosed, "channel closed");

        var results = await new SystemProbe().ProbeAsync(Context(runner), TestContext.CancellationToken);

        results.Single().Status.Should().Be(CapabilityStatus.Unknown);
        results.Single().Reason.Should().Contain("channel closed");
    }

    [TestMethod]
    public async Task CommandExecution_WhenMarkerEchoed_IsAvailable()
    {
        var runner = new ScriptedCommandRunner().Respond("echo termforge-probe", 0, "termforge-probe\n");

        var results = await new CommandExecutionProbe().ProbeAsync(Context(runner), TestContext.CancellationToken);

        results.Single().Status.Should().Be(CapabilityStatus.Available);
    }

    [TestMethod]
    public async Task CommandExecution_WhenChannelRefused_IsUnavailable()
    {
        var runner = new ScriptedCommandRunner().Fail("echo", SshErrorKind.ChannelFailed, "channel open failure");

        var results = await new CommandExecutionProbe().ProbeAsync(Context(runner), TestContext.CancellationToken);

        results.Single().Status.Should().Be(CapabilityStatus.Unavailable);
    }

    [TestMethod]
    public async Task Sftp_WhenSubsystemOpens_IsAvailableAndClientDisposed()
    {
        var client = Substitute.For<IRemoteFileClient>();
        var probe = new SftpProbe(CreateFileFactory(SshResult<IRemoteFileClient>.Success(client)));

        var results = await probe.ProbeAsync(Context(new ScriptedCommandRunner()), TestContext.CancellationToken);

        results.Single().Status.Should().Be(CapabilityStatus.Available);
        await client.Received(1).DisposeAsync();
    }

    [TestMethod]
    [DataRow(SshErrorKind.ChannelFailed, CapabilityStatus.Unavailable)]
    [DataRow(SshErrorKind.OperationFailed, CapabilityStatus.Unavailable)]
    [DataRow(SshErrorKind.ConnectionFailed, CapabilityStatus.Unknown)]
    [DataRow(SshErrorKind.AuthenticationFailed, CapabilityStatus.Unknown)]
    public async Task Sftp_WhenConnectFails_ClassifiesByErrorKind(SshErrorKind kind, CapabilityStatus expected)
    {
        var error = new SshError(kind, "boom");
        var probe = new SftpProbe(CreateFileFactory(SshResult<IRemoteFileClient>.Failure(error)));

        var results = await probe.ProbeAsync(Context(new ScriptedCommandRunner()), TestContext.CancellationToken);

        results.Single().Status.Should().Be(expected);
        results.Single().Reason.Should().NotBeNullOrWhiteSpace();
    }

    [TestMethod]
    public async Task Docker_WhenNotInstalled_BothResultsAreUnavailable()
    {
        var runner = new ScriptedCommandRunner().Respond("command -v docker", 127);

        var results = await new DockerProbe().ProbeAsync(Context(runner), TestContext.CancellationToken);

        results.Select(result => result.Status).Should().OnlyContain(status => status == CapabilityStatus.Unavailable);
        results[0].Reason.Should().Contain("not installed");
    }

    [TestMethod]
    public async Task Docker_WhenPermissionDenied_ExplainsDockerGroup()
    {
        var runner = new ScriptedCommandRunner()
            .Respond("command -v docker", 0)
            .Respond("docker info", 1, RecordedOutputs.DockerPermissionDenied);

        var results = await new DockerProbe().ProbeAsync(Context(runner), TestContext.CancellationToken);

        results[0].Status.Should().Be(CapabilityStatus.Available);
        results[1].Status.Should().Be(CapabilityStatus.Unavailable);
        results[1].Reason.Should().Contain("not in the docker group");
    }

    [TestMethod]
    public async Task Docker_WhenDaemonDown_IsUnavailableWithHint()
    {
        var runner = new ScriptedCommandRunner()
            .Respond("command -v docker", 0)
            .Respond("docker info", 1, RecordedOutputs.DockerDaemonDown);

        var results = await new DockerProbe().ProbeAsync(Context(runner), TestContext.CancellationToken);

        results[1].Status.Should().Be(CapabilityStatus.Unavailable);
        results[1].Reason.Should().Contain("not running");
    }

    [TestMethod]
    public async Task Docker_WhenAccessible_ReportsVersion()
    {
        var runner = new ScriptedCommandRunner()
            .Respond("command -v docker", 0)
            .Respond("docker info", 0, "27.3.1\n");

        var results = await new DockerProbe().ProbeAsync(Context(runner), TestContext.CancellationToken);

        results[1].Status.Should().Be(CapabilityStatus.Available);
        results[1].Reason.Should().Contain("27.3.1");
    }

    [TestMethod]
    public async Task Docker_WhenLookupFails_BothResultsAreUnknown()
    {
        var runner = new ScriptedCommandRunner().Fail("command -v docker", SshErrorKind.SessionClosed, "closed");

        var results = await new DockerProbe().ProbeAsync(Context(runner), TestContext.CancellationToken);

        results.Select(result => result.Status).Should().OnlyContain(status => status == CapabilityStatus.Unknown);
    }

    [TestMethod]
    public async Task Sudo_WhenMissing_IsUnavailable()
    {
        var runner = new ScriptedCommandRunner().Respond("command -v sudo", 1);

        var results = await new SudoProbe().ProbeAsync(Context(runner), TestContext.CancellationToken);

        results.Single().Status.Should().Be(CapabilityStatus.Unavailable);
        results.Single().Reason.Should().Contain("not installed");
    }

    [TestMethod]
    public async Task Sudo_Passwordless_IsAvailable()
    {
        var runner = new ScriptedCommandRunner().Respond("command -v sudo", 0).Respond("sudo -n true", 0);

        var results = await new SudoProbe().ProbeAsync(Context(runner), TestContext.CancellationToken);

        results.Single().Status.Should().Be(CapabilityStatus.Available);
    }

    [TestMethod]
    public async Task Sudo_NeedsPassword_IsUnavailableWithHint()
    {
        var runner = new ScriptedCommandRunner().Respond("command -v sudo", 0).Respond("sudo -n true", 1, RecordedOutputs.SudoPasswordRequired);

        var results = await new SudoProbe().ProbeAsync(Context(runner), TestContext.CancellationToken);

        results.Single().Status.Should().Be(CapabilityStatus.Unavailable);
        results.Single().Reason.Should().Contain("NOPASSWD");
    }

    [TestMethod]
    public async Task Utilities_OnMinimalContainer_ReportsMissingFreeAndUptime()
    {
        var runner = new ScriptedCommandRunner().Respond("for t in", 0, RecordedOutputs.MinimalContainerUtilities);

        var results = await new UtilitiesProbe().ProbeAsync(Context(runner), TestContext.CancellationToken);

        results.Should().HaveCount(4);
        results.Single(result => result.Id == "utility.stat").Status.Should().Be(CapabilityStatus.Available);
        results.Single(result => result.Id == "utility.free").Status.Should().Be(CapabilityStatus.Unavailable);
        results.Single(result => result.Id == "utility.free").Reason.Should().Contain("procps");
    }

    [TestMethod]
    public async Task Utilities_WithGarbageOutput_AreUnknown()
    {
        var runner = new ScriptedCommandRunner().Respond("for t in", 0, "\u0000\u0001 garbage");

        var results = await new UtilitiesProbe().ProbeAsync(Context(runner), TestContext.CancellationToken);

        results.Should().OnlyContain(result => result.Status == CapabilityStatus.Unknown);
    }

    [TestMethod]
    public async Task Utilities_WhenRunnerFails_AreUnknown()
    {
        var runner = new ScriptedCommandRunner().Fail("for t in", SshErrorKind.OperationFailed, "timed out");

        var results = await new UtilitiesProbe().ProbeAsync(Context(runner), TestContext.CancellationToken);

        results.Should().HaveCount(4);
        results.Should().OnlyContain(result => result.Status == CapabilityStatus.Unknown && result.Reason.Contains("timed out"));
    }

    private static CapabilityProbeContext Context(IRemoteCommandRunner runner)
    {
        return new CapabilityProbeContext(Target, runner);
    }

    private static IRemoteFileClientFactory CreateFileFactory(SshResult<IRemoteFileClient> result)
    {
        var factory = Substitute.For<IRemoteFileClientFactory>();
        factory.ConnectAsync(default!, default).ReturnsForAnyArgs(result);
        return factory;
    }
}
