using DTech.Core.NetworkDiagnostics;

namespace DTech.Entrypoint.NetworkDiagnostics;

public sealed record NetworkDiagnosticResult
{
    public required int Sequence { get; init; }
    public required NetworkDiagnosticPoint Point { get; init; }
    public required string PointName { get; init; }
    public required string Protocol { get; init; }
    public int? Port { get; init; }
    public required NetworkProbeOutcome Outcome { get; init; }
    public required double ElapsedMilliseconds { get; init; }
}

public sealed record NetworkDiagnosticSnapshot
{
    public required Guid Id { get; init; }
    public required string Host { get; init; }
    public required string Status { get; init; }
    public required int Completed { get; init; }
    public required int Total { get; init; }
    public required IReadOnlyList<NetworkDiagnosticPoint> Points { get; init; }
    public string? Error { get; init; }
}

public sealed class NetworkDiagnosticRun(Guid id, NetworkDiagnosticPlan plan) : IDisposable
{
    private readonly Lock _gate = new();
    private readonly List<NetworkDiagnosticResult> _results = [];
    private readonly CancellationTokenSource _stop = new();
    private string _status = "queued";
    private string? _error;

    public Guid Id { get; } = id;
    public NetworkDiagnosticPlan Plan { get; } = plan;
    public CancellationToken CancellationToken => _stop.Token;

    public bool Cancel()
    {
        lock (_gate)
        {
            if (_status is "completed" or "cancelled" or "failed") return false;
            if (_status == "queued") _status = "cancelled";
        }
        _stop.Cancel();
        return true;
    }

    public void SetStatus(string status, string? error = null)
    {
        lock (_gate) { _status = status; _error = error; }
    }

    public bool TryStart()
    {
        lock (_gate)
        {
            if (_status != "queued") return false;
            _status = "running";
            return true;
        }
    }

    public void Add(NetworkDiagnosticResult result)
    {
        lock (_gate) { _results.Add(result with { Sequence = _results.Count }); }
    }

    public NetworkDiagnosticSnapshot Snapshot()
    {
        lock (_gate)
        {
            return new NetworkDiagnosticSnapshot
            {
                Id = Id, Host = Plan.Host, Status = _status, Error = _error, Points = Plan.Points,
                Completed = _results.Count, Total = Plan.ProbesPerPoint * Plan.Points.Count,
            };
        }
    }

    public IReadOnlyList<NetworkDiagnosticResult> Page(int offset, int limit)
    {
        lock (_gate) { return _results.Skip(offset).Take(limit).ToArray(); }
    }

    public void Dispose() => _stop.Dispose();
}
