using System.Diagnostics;
using DTech.Entrypoint.NetworkDiagnostics;
using FluentAssertions;

namespace DTech.Entrypoint.Tests.Diagnostics;

[TestClass]
public sealed class NetworkProbeLimiterTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Concurrency_IsSharedAndWaitingLeaseCanBeCancelled()
    {
        using var limiter = new NetworkProbeLimiter();
        var leases = new List<IDisposable>();
        try
        {
            for (var i = 0; i < 16; i++) leases.Add(await limiter.AcquireAsync(null, TestContext.CancellationToken));
            using var stop = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
            var blocked = () => limiter.AcquireAsync(null, stop.Token);

            await blocked.Should().ThrowAsync<OperationCanceledException>();

            leases[0].Dispose();
            leases.RemoveAt(0);
            using var resumed = await limiter.AcquireAsync(null, TestContext.CancellationToken);
        }
        finally
        {
            for (var i = 0; i < leases.Count; i++) leases[i].Dispose();
        }
    }

    [TestMethod]
    public async Task Rate_PreventsMoreThanFiftyStartsPerSecond()
    {
        using var limiter = new NetworkProbeLimiter();
        var timer = Stopwatch.StartNew();

        for (var i = 0; i < 50; i++)
        {
            using var lease = await limiter.AcquireAsync(null, TestContext.CancellationToken);
        }

        timer.Elapsed.Should().BeGreaterThan(TimeSpan.FromMilliseconds(980));
    }
}
