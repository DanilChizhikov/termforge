using DTech.Core.AiBridge.Approvals;

namespace DTech.Core.Tests.AiBridge.Support;

internal sealed class ScriptedApprovalNotifier : IAiApprovalNotifier
{
    public AiApprovalBroker? Broker { get; set; }

    public AiApprovalOutcome? Answer { get; set; }

    public List<AiActionDescription> Requested { get; } = [];

    public List<int> PendingCounts { get; } = [];

    public Task PendingApprovalsChanged(IReadOnlyList<PendingAiApproval> pendingApprovals, CancellationToken cancellationToken)
    {
        PendingCounts.Add(pendingApprovals.Count);
        for (var i = 0; i < pendingApprovals.Count; i++)
        {
            Requested.Add(pendingApprovals[i].Action);
            if (Answer is { } answer)
            {
                Broker?.TryResolve(pendingApprovals[i].Id, answer);
            }
        }

        return Task.CompletedTask;
    }
}
