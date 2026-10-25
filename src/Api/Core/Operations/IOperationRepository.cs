namespace DTech.Core.Operations;

public interface IOperationRepository
{
    Task Add(Operation operation, CancellationToken cancellationToken);

    Task Update(Operation operation, CancellationToken cancellationToken);

    Task<Operation?> Find(Guid id, CancellationToken cancellationToken);

    Task<OperationPage> Query(OperationQuery query, CancellationToken cancellationToken);
}
