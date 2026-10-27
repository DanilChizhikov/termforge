using DTech.Core.Operations;
using DTech.Core.Transfers;

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
}
