namespace DTech.Core.Operations;

public interface IOperationRetryHandler
{
    string Kind { get; }

    Task Retry(Operation operation, CancellationToken cancellationToken);

    Task Retry(Operation operation, OperationRetryOptions options, CancellationToken cancellationToken)
    {
        return Retry(operation, cancellationToken);
    }
}
