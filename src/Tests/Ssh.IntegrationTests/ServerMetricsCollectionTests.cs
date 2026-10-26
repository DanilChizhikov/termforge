using DTech.Core.Diagnostics;
using DTech.Core.Monitoring;
using DTech.Ssh.IntegrationTests.Harness;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace DTech.Ssh.IntegrationTests;

[TestClass]
public sealed class ServerMetricsCollectionTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Collect_AgainstAlpineServer_ReadsRealMetrics()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        var verifier = SshTransportTestServices.CreatePinnedVerifier(cluster.Target.Identity.HostKey);
        await using var services = SshTransportTestServices.Create(verifier);
        var factory = services.GetRequiredService<IRemoteCommandRunnerFactory>();
        var connected = await factory.ConnectAsync(cluster.Target.CreateKeyTarget(), TestContext.CancellationToken);
        connected.IsSuccess.Should().BeTrue(connected.Error?.Message);
        await using var runner = connected.Value;

        var result = await new ServerMetricsCollector().CollectAsync(runner, TestContext.CancellationToken);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        var readings = result.Value;
        readings.Uptime.IsAvailable.Should().BeTrue(readings.Uptime.Reason);
        readings.CpuPercent.IsAvailable.Should().BeTrue(readings.CpuPercent.Reason);
        readings.CpuPercent.Value.Should().BeInRange(0, 100);
        readings.Memory.IsAvailable.Should().BeTrue(readings.Memory.Reason);
        readings.Memory.Value!.Value.UsedBytes.Should().BeInRange(0, readings.Memory.Value.Value.TotalBytes);
        readings.Disk.IsAvailable.Should().BeTrue(readings.Disk.Reason);
        readings.Disk.Value!.Value.MountPoint.Should().Be("/");
    }
}
