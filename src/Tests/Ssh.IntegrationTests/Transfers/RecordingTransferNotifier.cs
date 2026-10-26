using System.Collections.Concurrent;
using DTech.Core.Transfers;

namespace DTech.Ssh.IntegrationTests.Transfers;

internal sealed class RecordingTransferNotifier : ITransferNotifier
{
    public ConcurrentQueue<TransferSnapshot> Changes { get; } = new();

    public Task TransferChanged(TransferSnapshot snapshot, CancellationToken cancellationToken)
    {
        Changes.Enqueue(snapshot);
        return Task.CompletedTask;
    }

    public Task TransferRemoved(Guid id, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
