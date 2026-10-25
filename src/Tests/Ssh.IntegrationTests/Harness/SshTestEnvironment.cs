using DotNet.Testcontainers.Builders;

namespace DTech.Ssh.IntegrationTests.Harness;

internal static class SshTestEnvironment
{
    public const string SkipDockerTestsVariable = "TERMFORGE_SKIP_DOCKER_TESTS";

    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(5);
    private static readonly Lazy<Task<SshServerClusterStartup>> Startup = new(StartClusterAsync, LazyThreadSafetyMode.ExecutionAndPublication);

    public static async Task<SshServerCluster> RequireClusterAsync()
    {
        var skipReason = GetConfiguredSkipReason();
        if (skipReason is not null)
        {
            Assert.Inconclusive(skipReason);
        }

        var startup = await Startup.Value;
        if (startup.Cluster is null)
        {
            Assert.Inconclusive(startup.SkipReason ?? "SSH test server is not available");
        }

        return startup.Cluster!;
    }

    public static async Task StopAsync()
    {
        if (!Startup.IsValueCreated)
        {
            return;
        }

        var startup = await Startup.Value;
        if (startup.Cluster is not null)
        {
            await startup.Cluster.DisposeAsync();
        }
    }

    private static string? GetConfiguredSkipReason()
    {
        var flag = Environment.GetEnvironmentVariable(SkipDockerTestsVariable);
        var isSkipped = string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase) || flag == "1";
        return isSkipped ? $"Docker based SSH tests are disabled on this machine by {SkipDockerTestsVariable}={flag}" : null;
    }

    private static async Task<SshServerClusterStartup> StartClusterAsync()
    {
        using var timeout = new CancellationTokenSource(StartupTimeout);
        try
        {
            var cluster = await SshServerCluster.StartAsync(timeout.Token);
            return new SshServerClusterStartup(cluster, null);
        }
        catch (DockerUnavailableException exception)
        {
            return new SshServerClusterStartup(null, $"Docker is not available, SSH integration tests are skipped: {exception.Message}");
        }
    }

    private sealed record SshServerClusterStartup(SshServerCluster? Cluster, string? SkipReason);
}
