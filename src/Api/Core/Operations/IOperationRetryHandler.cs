namespace DTech.Core.Operations;

public interface IOperationRetryHandler
{
    string Kind { get; }

    Task Retry(Operation operation, CancellationToken cancellationToken);
}
