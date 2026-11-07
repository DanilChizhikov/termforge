using System.Collections.Concurrent;
using DTech.Core.NetworkDiagnostics;

namespace DTech.Entrypoint.NetworkDiagnostics;

public sealed record PendingNetworkAction(RemoteNetworkActionPlan Plan, DateTime ConnectionRevision, bool UsePassword);

public sealed class RemoteNetworkActionPlans(TimeProvider clock)
{
    private readonly ConcurrentDictionary<Guid, PendingNetworkAction> _plans = new();
    public void Add(PendingNetworkAction action)
    {
        var now = clock.GetUtcNow();
        foreach (var entry in _plans)
        {
            if (entry.Value.Plan.ExpiresAt <= now) _plans.TryRemove(entry.Key, out _);
        }
        if (_plans.Count >= 128) throw new InvalidOperationException("Too many pending remote actions. Wait for old confirmations to expire.");
        _plans[action.Plan.Id] = action;
    }
    public PendingNetworkAction? Find(Guid id)
    {
        return _plans.TryGetValue(id, out var action) && action.Plan.ExpiresAt > clock.GetUtcNow() ? action : null;
    }
    public bool Remove(Guid id) => _plans.TryRemove(id, out _);
}
