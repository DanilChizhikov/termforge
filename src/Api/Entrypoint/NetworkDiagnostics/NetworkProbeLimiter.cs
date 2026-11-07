using System.Collections.Concurrent;
using System.Diagnostics;

namespace DTech.Entrypoint.NetworkDiagnostics;

public sealed class NetworkProbeLimiter : IDisposable
{
    private readonly ConcurrentDictionary<Guid, PointLimit> _points = new();

    public async Task<IDisposable> AcquireAsync(Guid? connectionId, CancellationToken cancellationToken)
    {
        var point = _points.GetOrAdd(connectionId ?? Guid.Empty, static _ => new PointLimit());
        await point.Concurrency.WaitAsync(cancellationToken);
        try
        {
            await point.Rate.WaitAsync(cancellationToken);
            try
            {
                var wait = TimeSpan.FromMilliseconds(20) - Stopwatch.GetElapsedTime(point.Last);
                if (wait > TimeSpan.Zero)
                {
                    var delay = TimeSpan.FromMilliseconds(Math.Ceiling(wait.TotalMilliseconds));
                    await Task.Delay(delay, cancellationToken);
                }
                point.Last = Stopwatch.GetTimestamp();
            }
            finally { point.Rate.Release(); }
            return new Lease(point.Concurrency);
        }
        catch
        {
            point.Concurrency.Release();
            throw;
        }
    }

    public void Dispose()
    {
        foreach (var point in _points.Values) { point.Rate.Dispose(); point.Concurrency.Dispose(); }
    }

    private sealed class PointLimit
    {
        public SemaphoreSlim Concurrency { get; } = new(16, 16);
        public SemaphoreSlim Rate { get; } = new(1, 1);
        public long Last { get; set; } = Stopwatch.GetTimestamp();
    }

    private sealed class Lease(SemaphoreSlim semaphore) : IDisposable
    {
        public void Dispose() => semaphore.Release();
    }
}
