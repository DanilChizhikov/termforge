using System.Collections.Concurrent;

namespace DTech.Core.AiBridge.Approvals;

public sealed class AiApprovalBroker(AiApprovalTimeout timeout, IAiApprovalNotifier notifier, TimeProvider timeProvider)
{
    private readonly ConcurrentDictionary<Guid, PendingAiApproval> _pendingApprovals = new();

    public async Task<AiApprovalOutcome> RequestAsync(AiActionDescription action, CancellationToken cancellationToken)
    {
        var expiresAt = timeProvider.GetUtcNow() + timeout.Value;
        var pendingApproval = new PendingAiApproval(Guid.NewGuid(), action, expiresAt);
        _pendingApprovals[pendingApproval.Id] = pendingApproval;
        try
        {
            await NotifyAsync();
            return await pendingApproval.Completion.WaitAsync(timeout.Value, timeProvider, cancellationToken);
        }
        catch (TimeoutException)
        {
            return AiApprovalOutcome.TimedOut;
        }
        finally
        {
            _pendingApprovals.TryRemove(pendingApproval.Id, out _);
            await NotifyAsync();
        }
    }

    public IReadOnlyList<PendingAiApproval> GetPending()
    {
        return _pendingApprovals.Values.OrderBy(approval => approval.ExpiresAt).ToList();
    }

    public bool TryResolve(Guid id, AiApprovalOutcome outcome)
    {
        return _pendingApprovals.TryGetValue(id, out var pendingApproval) && pendingApproval.TryComplete(outcome);
    }

    private Task NotifyAsync()
    {
        var pendingApprovals = GetPending();
        return notifier.PendingApprovalsChanged(pendingApprovals, CancellationToken.None);
    }
}
