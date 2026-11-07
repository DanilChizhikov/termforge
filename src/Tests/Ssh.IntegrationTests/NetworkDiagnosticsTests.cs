using DTech.Core.Diagnostics;
using DTech.Core.NetworkDiagnostics;
using DTech.Ssh.IntegrationTests.Harness;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace DTech.Ssh.IntegrationTests;

[TestClass]
public sealed class NetworkDiagnosticsTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task LocalSsh_PerformsKeyExchangeAgainstTestServerWithoutLogin()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        await using var session = new LocalNetworkProbeSession();
        var probe = new NetworkProbe { Host = cluster.Target.Endpoint.Host, Domain = "localhost", Protocol = "ssh", Port = cluster.Target.Endpoint.Port };

        var result = await session.ProbeAsync(probe, TestContext.CancellationToken);

        if (result.Status == NetworkProbeOutcome.Unavailable) Assert.Inconclusive(result.Detail);
        result.Status.Should().Be(NetworkProbeOutcome.Responded);
        result.Detail.Should().Contain("without login");
    }

    [TestMethod]
    public async Task RemoteSession_MissingTools_AreReportedWithoutStoppingOtherChecks()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        var verifier = SshTransportTestServices.CreatePinnedVerifier(cluster.Target.Identity.HostKey);
        await using var services = SshTransportTestServices.Create(verifier);
        var factory = services.GetRequiredService<IRemoteCommandRunnerFactory>();
        var target = cluster.Target.CreateKeyTarget();
        var opened = await factory.ConnectAsync(target, TestContext.CancellationToken);
        await using var session = new RemoteNetworkProbeSession(opened.Value);
        await session.InitializeAsync(TestContext.CancellationToken);
        var tlsProbe = new NetworkProbe { Host = "127.0.0.1", Domain = "localhost", Protocol = "tls", Port = 22 };
        var routeProbe = tlsProbe with { Protocol = "route", Port = null };

        var tls = await session.ProbeAsync(tlsProbe, TestContext.CancellationToken);
        var route = await session.ProbeAsync(routeProbe, TestContext.CancellationToken);

        tls.Status.Should().BeOneOf(NetworkProbeOutcome.Unavailable, NetworkProbeOutcome.Error);
        route.Status.Should().BeOneOf(NetworkProbeOutcome.Unavailable, NetworkProbeOutcome.Responded, NetworkProbeOutcome.NoResponse);
    }
}
