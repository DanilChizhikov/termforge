using System.Collections.Concurrent;
using DTech.Core.Operations;
using DTech.Core.Transfers;

namespace DTech.Entrypoint.Tests.Lifecycle.Support;

internal sealed class RecordingTransferJournal(ITransferJournal inner) : ITransferJournal
{
    private readonly ConcurrentQueue<OperationStatus> _journaledOutcomes = new();

    public IReadOnlyList<OperationStatus> JournaledOutcomes => _journaledOutcomes.ToList();

    public Task<Guid> CreateAsync(TransferRequest request, string targetPath, CancellationToken cancellationToken)
    {
        return inner.CreateAsync(request, targetPath, cancellationToken);
    }

    public Task StartAsync(Guid id, CancellationToken cancellationToken)
    {
        return inner.StartAsync(id, cancellationToken);
    }

    public async Task SucceedAsync(Guid id, TransferProgress progress, CancellationToken cancellationToken)
    {
        await inner.SucceedAsync(id, progress, cancellationToken);
        _journaledOutcomes.Enqueue(OperationStatus.Succeeded);
    }

    public async Task FailAsync(Guid id, TransferFailure failure, CancellationToken cancellationToken)
    {
        await inner.FailAsync(id, failure, cancellationToken);
        _journaledOutcomes.Enqueue(OperationStatus.Failed);
    }

    public async Task CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        await inner.CancelAsync(id, cancellationToken);
        _journaledOutcomes.Enqueue(OperationStatus.Cancelled);
    }
}
