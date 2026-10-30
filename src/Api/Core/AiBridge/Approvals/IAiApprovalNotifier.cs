namespace DTech.Core.AiBridge.Approvals;

public interface IAiApprovalNotifier
{
    Task PendingApprovalsChanged(IReadOnlyList<PendingAiApproval> pendingApprovals, CancellationToken cancellationToken);
}
