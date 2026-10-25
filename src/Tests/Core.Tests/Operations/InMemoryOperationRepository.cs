using DTech.Core.Operations;

namespace DTech.Core.Tests.Operations;

internal sealed class InMemoryOperationRepository : IOperationRepository
{
    private readonly Dictionary<Guid, Operation> _operations = new();

    public Task Add(Operation operation, CancellationToken cancellationToken)
    {
        _operations[operation.Id] = operation;
        return Task.CompletedTask;
    }

    public Task Update(Operation operation, CancellationToken cancellationToken)
    {
        _operations[operation.Id] = operation;
        return Task.CompletedTask;
    }

    public Task<Operation?> Find(Guid id, CancellationToken cancellationToken)
    {
        _operations.TryGetValue(id, out var operation);
        return Task.FromResult(operation);
    }

    public Task<OperationPage> Query(OperationQuery query, CancellationToken cancellationToken)
    {
        var items = _operations.Values.ToList();
        var page = new OperationPage(items, items.Count, query.Offset, query.Limit);
        return Task.FromResult(page);
    }
}
