using DTech.Entrypoint.Realtime;
using DTech.HostKeys.Decisions;

namespace DTech.Entrypoint.HostKeys;

internal sealed class HostKeyBroadcaster(RealtimeChannel channel) : IPendingHostKeyDecisionNotifier
{
    public const string MessageType = "hostKeys.pendingChanged";

    public Task PendingDecisionsChanged(IReadOnlyList<PendingHostKeyDecision> pendingDecisions, CancellationToken cancellationToken)
    {
        var responses = PendingHostKeyResponseMapper.Map(pendingDecisions);
        return channel.Publish(MessageType, responses, cancellationToken);
    }
}
