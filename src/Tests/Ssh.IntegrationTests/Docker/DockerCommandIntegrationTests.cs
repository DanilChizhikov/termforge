using System.Diagnostics;
using System.Text;
using DTech.Core.Diagnostics;
using DTech.Core.Docker;
using DTech.Ssh.IntegrationTests.Harness;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace DTech.Ssh.IntegrationTests.Docker;

[TestClass]
public sealed class DockerCommandIntegrationTests
{
    private static readonly TimeSpan PromptStopLimit = TimeSpan.FromSeconds(5);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task CommandStream_RelaysOutputUntilExitAndReportsExitCode()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        await using var services = CreateServices(cluster);
        var streamFactory = services.GetRequiredService<IRemoteCommandStreamFactory>();
        var target = cluster.Target.CreateKeyTarget();

        var started = await streamFactory.StartAsync(target, "for i in 1 2 3; do echo line$i; sleep 0.1; done; echo oops 1>&2; exit 4", TestContext.CancellationToken);

        started.IsSuccess.Should().BeTrue(started.Error?.Message);
        await using var stream = new ContainerLogStream(started.Value);
        var output = await ReadToEndAsync(stream);
        output.Should().Contain("line1\nline2\nline3\n");
        stream.ExitCode.Should().Be(4);
    }

    [TestMethod]
    public async Task CommandStream_WhenDisposedDuringEndlessFollow_StopsPromptly()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        await using var services = CreateServices(cluster);
        var streamFactory = services.GetRequiredService<IRemoteCommandStreamFactory>();
        var target = cluster.Target.CreateKeyTarget();
        var started = await streamFactory.StartAsync(target, "while true; do echo tick; sleep 0.2; done", TestContext.CancellationToken);
        started.IsSuccess.Should().BeTrue(started.Error?.Message);
        var stream = new ContainerLogStream(started.Value);
        var first = await stream.ReadAsync(TestContext.CancellationToken);
        first.Should().Contain("tick");

        var stopwatch = Stopwatch.StartNew();
        await stream.DisposeAsync();

        stopwatch.Elapsed.Should().BeLessThan(PromptStopLimit);
    }

    [TestMethod]
    public async Task ContainerList_OnServerWithoutDocker_ReportsDockerMissing()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        await using var services = CreateServices(cluster);
        var runnerFactory = services.GetRequiredService<IRemoteCommandRunnerFactory>();
        var target = cluster.Target.CreateKeyTarget();
        var connected = await runnerFactory.ConnectAsync(target, TestContext.CancellationToken);
        connected.IsSuccess.Should().BeTrue(connected.Error?.Message);
        await using var runner = connected.Value;
        var lister = new DockerContainerLister();

        var listed = await lister.ListAsync(runner, TestContext.CancellationToken);

        listed.Error!.Code.Should().Be(DockerErrorCode.DockerMissing);
        listed.Error.Message.Should().Contain("not installed");
    }

    private static ServiceProvider CreateServices(SshServerCluster cluster)
    {
        var verifier = SshTransportTestServices.CreatePinnedVerifier(cluster.Target.Identity.HostKey);
        return SshTransportTestServices.Create(verifier);
    }

    private async Task<string> ReadToEndAsync(ContainerLogStream stream)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        var output = new StringBuilder();
        var chunk = await stream.ReadAsync(timeout.Token);
        while (chunk is not null)
        {
            output.Append(chunk);
            chunk = await stream.ReadAsync(timeout.Token);
        }

        return output.ToString();
    }
}
