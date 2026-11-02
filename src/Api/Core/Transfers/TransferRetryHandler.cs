using DTech.Core.Files;
using DTech.Core.Operations;

namespace DTech.Core.Transfers;

public sealed class TransferRetryHandler(string kind, TransferEnqueuer enqueuer) : IOperationRetryHandler
{
    public string Kind { get; } = kind;

    public Task Retry(Operation operation, CancellationToken cancellationToken)
    {
        var options = new OperationRetryOptions();
        return Retry(operation, options, cancellationToken);
    }

    public async Task Retry(Operation operation, OperationRetryOptions options, CancellationToken cancellationToken)
    {
        var result = await enqueuer.RetryAsync(operation, options, cancellationToken);
        if (result.Conflicts.Count > 0)
        {
            throw new OperationException(OperationFailure.TransferConflict, "Target items already exist. Confirm overwriting to retry.")
            {
                Conflicts = result.Conflicts,
            };
        }

        if (result.Error is not null)
        {
            var failure = result.Error.Code switch
            {
                FileErrorCode.ConfirmationRequired => OperationFailure.ConfirmationRequired,
                FileErrorCode.ReadOnlyServer => OperationFailure.ReadOnlyServer,
                FileErrorCode.AlreadyExists => OperationFailure.TransferConflict,
                _ => OperationFailure.InvalidRequest,
            };
            throw new OperationException(failure, result.Error.Message);
        }
    }
}
