using DTech.Core.Connections;
using DTech.Core.Diagnostics;
using DTech.Core.Diagnostics.Probes;
using DTech.Core.Files;
using DTech.Ssh.IntegrationTests.Harness;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace DTech.Ssh.IntegrationTests;

[TestClass]
public sealed class RemoteCommandRunnerTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Run_ReturnsStdoutStderrAndExitCode()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        await using var services = CreateServices(cluster);
        await using var runner = await ConnectAsync(services, cluster);
        var request = RemoteCommandRequest.WithDefaults("echo out; echo err 1>&2; exit 3");

        var result = await runner.RunAsync(request, TestContext.CancellationToken);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.ExitCode.Should().Be(3);
        result.Value.StandardOutput.Trim().Should().Be("out");
        result.Value.StandardError.Trim().Should().Be("err");
        result.Value.OutputTruncated.Should().BeFalse();
    }

    [TestMethod]
    public async Task Run_WhenOutputExceedsLimit_TruncatesAndFlags()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        await using var services = CreateServices(cluster);
        await using var runner = await ConnectAsync(services, cluster);
        var request = new RemoteCommandRequest("head -c 5000000 /dev/zero | tr '\\0' x", TimeSpan.FromSeconds(20), 4096);

        var result = await runner.RunAsync(request, TestContext.CancellationToken);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.OutputTruncated.Should().BeTrue();
        result.Value.StandardOutput.Length.Should().BeLessThanOrEqualTo(4096);
    }

    [TestMethod]
    public async Task Run_WhenCommandOutlivesTimeout_ReturnsFailure()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        await using var services = CreateServices(cluster);
        await using var runner = await ConnectAsync(services, cluster);
        var request = new RemoteCommandRequest("sleep 30", TimeSpan.FromMilliseconds(500), 4096);

        var result = await runner.RunAsync(request, TestContext.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Should().Contain("timed out");
    }

    [TestMethod]
    public async Task Probes_AgainstAlpineServer_ReportCapabilitiesWithReasons()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        await using var services = CreateServices(cluster);
        await using var runner = await ConnectAsync(services, cluster);
        var fileFactory = services.GetRequiredService<IRemoteFileClientFactory>();
        var context = new CapabilityProbeContext(cluster.Target.CreateKeyTarget(), runner);
        ICapabilityProbe[] probes =
        [
            new CommandExecutionProbe(),
            new SystemProbe(),
            new SftpProbe(fileFactory),
            new DockerProbe(),
            new SudoProbe(),
            new UtilitiesProbe(),
        ];

        var results = new List<CapabilityResult>();
        for (var i = 0; i < probes.Length; i++)
        {
            var probeResults = await probes[i].ProbeAsync(context, TestContext.CancellationToken);
            results.AddRange(probeResults);
        }

        results.Single(result => result.Id == CapabilityIds.CommandExecution).Status.Should().Be(CapabilityStatus.Available);
        results.Single(result => result.Id == CapabilityIds.System).Reason.Should().Contain("Alpine");
        results.Single(result => result.Id == CapabilityIds.Sftp).Status.Should().Be(CapabilityStatus.Available);
        results.Single(result => result.Id == CapabilityIds.DockerInstalled).Status.Should().Be(CapabilityStatus.Unavailable);
        results.Single(result => result.Id == CapabilityIds.DockerAccess).Reason.Should().Contain("not installed");
        results.Single(result => result.Id == CapabilityIds.Sudo).Status.Should().Be(CapabilityStatus.Unavailable);
        results.Where(result => result.Id.StartsWith(CapabilityIds.UtilityPrefix, StringComparison.Ordinal)).Should().HaveCount(4)
            .And.OnlyContain(result => result.Status == CapabilityStatus.Available);
    }

    private static ServiceProvider CreateServices(SshServerCluster cluster)
    {
        var verifier = SshTransportTestServices.CreatePinnedVerifier(cluster.Target.Identity.HostKey);
        return SshTransportTestServices.Create(verifier);
    }

    private static async Task<IRemoteCommandRunner> ConnectAsync(ServiceProvider services, SshServerCluster cluster)
    {
        var factory = services.GetRequiredService<IRemoteCommandRunnerFactory>();
        var target = cluster.Target.CreateKeyTarget();
        var result = await factory.ConnectAsync(target, CancellationToken.None);
        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        return result.Value;
    }
}
