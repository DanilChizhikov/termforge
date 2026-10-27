using DTech.Core.Operations;

namespace DTech.Core.Transfers;

public sealed class TransferRetryHandler(string kind, TransferEnqueuer enqueuer) : IOperationRetryHandler
{
    public string Kind { get; } = kind;

    public async Task Retry(Operation operation, CancellationToken cancellationToken)
    {
        var result = await enqueuer.RetryAsync(operation, cancellationToken);
        if (result.Error is not null)
        {
            throw new OperationException(OperationFailure.InvalidRequest, result.Error.Message);
        }
    }
}
