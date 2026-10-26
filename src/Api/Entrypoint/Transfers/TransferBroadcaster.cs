using DTech.Core.Transfers;
using DTech.Entrypoint.Realtime;

namespace DTech.Entrypoint.Transfers;

internal sealed class TransferBroadcaster(RealtimeChannel channel) : ITransferNotifier
{
    public const string ChangedMessageType = "transfer.changed";
    public const string RemovedMessageType = "transfer.removed";

    public Task TransferChanged(TransferSnapshot snapshot, CancellationToken cancellationToken)
    {
        var response = TransferResponse.From(snapshot);
        return channel.Publish(ChangedMessageType, response, cancellationToken);
    }

    public Task TransferRemoved(Guid id, CancellationToken cancellationToken)
    {
        var payload = new TransferRemovedMessage(id);
        return channel.Publish(RemovedMessageType, payload, cancellationToken);
    }
}
