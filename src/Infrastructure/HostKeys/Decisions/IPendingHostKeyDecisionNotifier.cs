namespace DTech.HostKeys.Decisions;

public interface IPendingHostKeyDecisionNotifier
{
    Task PendingDecisionsChanged(IReadOnlyList<PendingHostKeyDecision> pendingDecisions, CancellationToken cancellationToken);
}
