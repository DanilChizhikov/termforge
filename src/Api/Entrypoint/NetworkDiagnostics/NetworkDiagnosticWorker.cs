using System.Diagnostics;
using DTech.Core.Diagnostics;
using DTech.Core.NetworkDiagnostics;
using DTech.Entrypoint.Realtime;

namespace DTech.Entrypoint.NetworkDiagnostics;

internal sealed class NetworkDiagnosticWorker(NetworkDiagnosticStore store, IServiceScopeFactory scopes, RealtimeChannel channel) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var first = ConsumeAsync(stoppingToken);
        var second = ConsumeAsync(stoppingToken);
        return Task.WhenAll(first, second);
    }

    private async Task ConsumeAsync(CancellationToken stoppingToken)
    {
        await foreach (var run in store.Pending.ReadAllAsync(stoppingToken))
        {
            if (!run.TryStart())
            {
                await PublishAsync(run);
                continue;
            }
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, run.CancellationToken);
            await PublishAsync(run);
            try
            {
                var tasks = new Task[run.Plan.Points.Count];
                for (var i = 0; i < tasks.Length; i++) tasks[i] = PointAsync(run, run.Plan.Points[i], stop.Token);
                var work = Task.WhenAll(tasks);
                while (!work.IsCompleted)
                {
                    await Task.WhenAny(work, Task.Delay(250, CancellationToken.None));
                    await PublishAsync(run);
                }
                await work;
                run.SetStatus(stop.IsCancellationRequested ? "cancelled" : "completed");
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { run.SetStatus("cancelled"); }
            catch (Exception exception) { run.SetStatus("failed", RemoteText.Sanitize(exception.Message, 1000)); }
            await PublishAsync(run);
        }
    }

    private Task PublishAsync(NetworkDiagnosticRun run)
    {
        var snapshot = run.Snapshot();
        return channel.Publish("network-diagnostics.changed", snapshot, CancellationToken.None);
    }

    private async Task PointAsync(NetworkDiagnosticRun run, NetworkDiagnosticPoint point, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<NetworkProbeSessionFactory>();
        NetworkProbeConnection connected;
        try
        {
            connected = await factory.OpenAsync(point, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            foreach (var probe in NetworkDiagnosticPlanner.Probes(run.Plan))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var outcome = new NetworkProbeOutcome(NetworkProbeOutcome.Error, RemoteText.Sanitize(exception.Message, 1000));
                Add(run, new ProbeReport(point, point.ConnectionId?.ToString() ?? "Local machine", probe), outcome);
            }
            return;
        }
        await using var session = connected.Session;
        var limiter = scope.ServiceProvider.GetRequiredService<NetworkProbeLimiter>();
        var options = new ParallelOptions { MaxDegreeOfParallelism = point.ConnectionId is null ? 16 : 4, CancellationToken = cancellationToken };
        var probes = NetworkDiagnosticPlanner.Probes(run.Plan);
        await Parallel.ForEachAsync(probes, options, async (probe, token) =>
        {
            using var lease = await limiter.AcquireAsync(point.ConnectionId, token);
            var started = Stopwatch.GetTimestamp();
            NetworkProbeOutcome outcome;
            try { outcome = await session.ProbeAsync(probe, token); }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                outcome = new NetworkProbeOutcome(NetworkProbeOutcome.Error, RemoteText.Sanitize(exception.Message, 1000));
            }
            var report = new ProbeReport(point, connected.Name, probe) { ElapsedMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds };
            Add(run, report, outcome);
        });
    }

    private static void Add(NetworkDiagnosticRun run, ProbeReport report, NetworkProbeOutcome outcome)
    {
        var result = new NetworkDiagnosticResult
        {
            Sequence = 0, Point = report.Point, PointName = report.Name, Protocol = report.Probe.Protocol,
            Port = report.Probe.Port, Outcome = outcome, ElapsedMilliseconds = report.ElapsedMilliseconds,
        };
        run.Add(result);
    }

    private sealed record ProbeReport(NetworkDiagnosticPoint Point, string Name, NetworkProbe Probe)
    {
        public double ElapsedMilliseconds { get; init; }
    }
}
