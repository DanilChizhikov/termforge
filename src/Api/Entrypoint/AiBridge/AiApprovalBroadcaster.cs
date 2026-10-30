using DTech.Core.AiBridge.Approvals;
using DTech.Entrypoint.Realtime;

namespace DTech.Entrypoint.AiBridge;

internal sealed class AiApprovalBroadcaster(RealtimeChannel channel) : IAiApprovalNotifier
{
    public const string MessageType = "aiBridge.approvalsChanged";

    public Task PendingApprovalsChanged(IReadOnlyList<PendingAiApproval> pendingApprovals, CancellationToken cancellationToken)
    {
        var responses = AiApprovalResponse.FromAll(pendingApprovals);
        return channel.Publish(MessageType, responses, cancellationToken);
    }
}
