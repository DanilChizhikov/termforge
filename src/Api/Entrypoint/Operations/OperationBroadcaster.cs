using DTech.Core.Operations;
using DTech.Entrypoint.Realtime;

namespace DTech.Entrypoint.Operations;

internal sealed class OperationBroadcaster : IOperationNotifier
{
    public const string MessageType = "operation.changed";

    private readonly RealtimeChannel _channel;

    public OperationBroadcaster(RealtimeChannel channel)
    {
        _channel = channel;
    }

    public Task OperationChanged(Operation operation, CancellationToken cancellationToken)
    {
        var response = OperationResponse.From(operation);
        return _channel.Publish(MessageType, response, cancellationToken);
    }
}
