using System.Collections.Concurrent;
using System.Threading.Channels;
using DTech.Core.NetworkDiagnostics;

namespace DTech.Entrypoint.NetworkDiagnostics;

public sealed class NetworkDiagnosticStore : IDisposable
{
    private readonly ConcurrentDictionary<Guid, NetworkDiagnosticRun> _runs = new();
    private readonly Channel<NetworkDiagnosticRun> _pending = Channel.CreateBounded<NetworkDiagnosticRun>(8);

    public ChannelReader<NetworkDiagnosticRun> Pending => _pending.Reader;

    public NetworkDiagnosticRun? Find(Guid id) => _runs.GetValueOrDefault(id);

    public NetworkDiagnosticSnapshot[] List() => _runs.Values.Select(run => run.Snapshot()).ToArray();

    public NetworkDiagnosticRun Create(NetworkDiagnosticPlan plan)
    {
        var id = Guid.NewGuid();
        var run = new NetworkDiagnosticRun(id, plan);
        _runs[id] = run;
        if (!_pending.Writer.TryWrite(run))
        {
            _runs.TryRemove(id, out _);
            run.Dispose();
            throw new InvalidOperationException("The diagnostics queue is full. Wait for a running check to finish.");
        }
        return run;
    }

    public void Dispose()
    {
        _pending.Writer.TryComplete();
        foreach (var run in _runs.Values) { run.Cancel(); run.Dispose(); }
    }
}
