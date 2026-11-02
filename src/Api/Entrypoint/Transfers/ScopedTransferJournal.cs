using DTech.Core.Operations;
using DTech.Core.Transfers;
using DTech.Core.Transfers.Queue;
using System.Text.Json;

namespace DTech.Entrypoint.Transfers;

internal sealed class ScopedTransferJournal(IServiceScopeFactory scopeFactory) : ITransferJournal
{
    public async Task<Guid> CreateAsync(TransferRequest request, string targetPath, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var journal = scope.ServiceProvider.GetRequiredService<IOperationJournal>();
        var kind = TransferOperationKinds.For(request.Direction);
        var details = TransferOperationDetails.Describe(request, targetPath);
        var create = new CreateOperationRequest(kind, request.ServerName, details, true);
        var operation = await journal.Create(create, cancellationToken);
        return operation.Id;
    }

    public async Task StartAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var journal = scope.ServiceProvider.GetRequiredService<IOperationJournal>();
        await journal.Start(id, cancellationToken);
    }

    public async Task SucceedAsync(Guid id, TransferProgress progress, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var journal = scope.ServiceProvider.GetRequiredService<IOperationJournal>();
        var summary = TransferOperationDetails.Summarize(progress);
        await journal.Succeed(id, summary, cancellationToken);
    }

    public async Task FailAsync(Guid id, TransferFailure failure, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var journal = scope.ServiceProvider.GetRequiredService<IOperationJournal>();
        var summary = TransferOperationDetails.Summarize(failure);
        var request = new FailOperationRequest(failure.Error, failure.Retryable, summary);
        await journal.Fail(id, request, cancellationToken);
    }

    public async Task CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var journal = scope.ServiceProvider.GetRequiredService<IOperationJournal>();
        await journal.Cancel(id, cancellationToken);
    }

    public async Task CheckpointAsync(TransferJob job, CancellationToken cancellationToken)
    {
        var state = job.CaptureRecovery();
        var data = JsonSerializer.Serialize(state);
        var checkpoint = new OperationCheckpoint(job.Id, data, state.PartialPaths.Count > 0);
        await using var scope = scopeFactory.CreateAsyncScope();
        var maintenance = scope.ServiceProvider.GetRequiredService<IOperationMaintenance>();
        await maintenance.CheckpointAsync(checkpoint, cancellationToken);
    }

    public async Task ForgetRecoveryAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IOperationRepository>();
        var operation = await repository.Find(id, cancellationToken);
        var state = operation is null ? null : TransferRecoveryState.TryRead(operation);
        var cleared = state is null ? null : state with { PartialPaths = [], PartialBytes = 0 };
        var data = cleared is null ? operation?.RecoveryData : JsonSerializer.Serialize(cleared);
        var checkpoint = new OperationCheckpoint(id, data, false);
        var maintenance = scope.ServiceProvider.GetRequiredService<IOperationMaintenance>();
        await maintenance.CheckpointAsync(checkpoint, cancellationToken);
        if (operation is not null)
        {
            var updated = operation with { RecoveryData = data, HasPendingCleanup = false };
            var notifier = scope.ServiceProvider.GetRequiredService<IOperationNotifier>();
            await notifier.OperationChanged(updated, cancellationToken);
        }
    }
}
