namespace DTech.Core.Safety;

public sealed class ServerWriteGate(IServerAccessModeReader accessModes, IConfirmationTokenStore confirmations)
{
    public const string ReadOnlyMessage = "This server is read-only: uploads, saves, deletes, renames and Docker actions are blocked.";

    public Task<bool> IsReadOnlyAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        return accessModes.IsReadOnlyAsync(connectionId, cancellationToken);
    }

    public async Task<WriteDenial?> CheckAsync(WriteIntent intent, CancellationToken cancellationToken)
    {
        if (await accessModes.IsReadOnlyAsync(intent.ConnectionId, cancellationToken))
        {
            return WriteDenial.ReadOnlyServer;
        }

        if (intent.Operation is not { } operation)
        {
            return null;
        }

        var scope = new ConfirmationScope(operation, intent.ConnectionId, intent.Target);
        return confirmations.TryConsume(scope, intent.ConfirmationToken) ? null : WriteDenial.ConfirmationRequired;
    }
}
