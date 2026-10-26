namespace DTech.Core.Operations;

public interface IOperationJournal
{
    Task<Operation> Create(CreateOperationRequest request, CancellationToken cancellationToken);

    Task<Operation> Start(Guid id, CancellationToken cancellationToken);

    Task<Operation> Succeed(Guid id, IReadOnlyDictionary<string, string>? details, CancellationToken cancellationToken);

    Task<Operation> Fail(Guid id, string error, CancellationToken cancellationToken);

    Task<Operation> Fail(Guid id, FailOperationRequest request, CancellationToken cancellationToken);

    Task<Operation> Cancel(Guid id, CancellationToken cancellationToken);

    Task<Operation?> Find(Guid id, CancellationToken cancellationToken);

    Task<OperationPage> List(OperationQuery query, CancellationToken cancellationToken);

    Task Retry(Guid id, CancellationToken cancellationToken);
}
