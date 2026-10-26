namespace DTech.HostKeys.Decisions;

internal sealed class NullPendingHostKeyDecisionNotifier : IPendingHostKeyDecisionNotifier
{
    public Task PendingDecisionsChanged(IReadOnlyList<PendingHostKeyDecision> pendingDecisions, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
