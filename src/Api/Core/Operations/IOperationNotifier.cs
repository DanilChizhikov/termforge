namespace DTech.Core.Operations;

public interface IOperationNotifier
{
    Task OperationChanged(Operation operation, CancellationToken cancellationToken);
}
