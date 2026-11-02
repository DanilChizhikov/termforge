using DTech.Core.SavedConnections;

namespace DTech.Core.Diagnostics;

public sealed class CapabilityReportCache
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);
    private readonly Dictionary<Guid, CachedReport> _reports = new();
    private readonly Lock _gate = new();
    private readonly TimeProvider _timeProvider;
    private long _generation;

    public CapabilityReportCache() : this(TimeProvider.System)
    {
    }

    public CapabilityReportCache(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public long Generation
    {
        get
        {
            lock (_gate)
            {
                return _generation;
            }
        }
    }

    public CapabilityReport? Find(Guid connectionId)
    {
        lock (_gate)
        {
            if (!_reports.TryGetValue(connectionId, out var cached))
            {
                return null;
            }

            var elapsed = _timeProvider.GetElapsedTime(cached.StoredAt);
            if (elapsed < Lifetime)
            {
                return cached.Report;
            }

            _reports.Remove(connectionId);
            return null;
        }
    }

    public void Store(CapabilityReport report)
    {
        var generation = Generation;
        Store(report, generation);
    }

    public bool Store(CapabilityReport report, long generation)
    {
        lock (_gate)
        {
            if (generation != _generation)
            {
                return false;
            }

            var storedAt = _timeProvider.GetTimestamp();
            _reports[report.ConnectionId] = new CachedReport(report, storedAt);
            return true;
        }
    }

    public void Invalidate(Guid connectionId, IReadOnlyList<SavedConnection> connections)
    {
        var dependents = connections.Where(connection => connection.Details.JumpHostId.HasValue)
            .ToLookup(connection => connection.Details.JumpHostId!.Value, connection => connection.Id);
        var invalidated = new HashSet<Guid> { connectionId };
        var pending = new Queue<Guid>();
        pending.Enqueue(connectionId);
        while (pending.TryDequeue(out var parent))
        {
            foreach (var dependent in dependents[parent])
            {
                if (invalidated.Add(dependent))
                {
                    pending.Enqueue(dependent);
                }
            }
        }

        lock (_gate)
        {
            _generation++;
            foreach (var id in invalidated)
            {
                _reports.Remove(id);
            }
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _generation++;
            _reports.Clear();
        }
    }

    private sealed record CachedReport(CapabilityReport Report, long StoredAt);
}
