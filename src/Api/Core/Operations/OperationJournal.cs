namespace DTech.Core.Operations;

public sealed class OperationJournal : IOperationJournal
{
    private readonly IOperationRepository _repository;
    private readonly IOperationSanitizer _sanitizer;
    private readonly IOperationNotifier _notifier;
    private readonly IReadOnlyDictionary<string, IOperationRetryHandler> _retryHandlers;
    private readonly TimeProvider _timeProvider;

    public OperationJournal(
        IOperationRepository repository,
        IOperationSanitizer sanitizer,
        IOperationNotifier notifier,
        IEnumerable<IOperationRetryHandler> retryHandlers,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _sanitizer = sanitizer;
        _notifier = notifier;
        _retryHandlers = IndexHandlers(retryHandlers);
        _timeProvider = timeProvider;
    }

    public async Task<Operation> Create(CreateOperationRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Kind))
        {
            throw new OperationException(OperationFailure.InvalidRequest, "the operation kind is required.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var details = _sanitizer.SanitizeDetails(request.Details);
        var operation = new Operation
        {
            Id = Guid.NewGuid(),
            Kind = request.Kind.Trim(),
            Server = request.Server,
            Status = OperationStatus.Queued,
            CreatedAt = now,
            UpdatedAt = now,
            Details = details,
            Retryable = request.Retryable,
        };
        await _repository.Add(operation, cancellationToken);
        await _notifier.OperationChanged(operation, cancellationToken);
        return operation;
    }

    public async Task<Operation> Start(Guid id, CancellationToken cancellationToken)
    {
        var current = await Load(id, cancellationToken);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var started = Transition(current, OperationStatus.Running, now) with { StartedAt = now };
        return await Save(started, cancellationToken);
    }

    public async Task<Operation> Succeed(Guid id, IReadOnlyDictionary<string, string>? details, CancellationToken cancellationToken)
    {
        var current = await Load(id, cancellationToken);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var finished = Transition(current, OperationStatus.Succeeded, now) with { FinishedAt = now };
        var merged = MergeDetails(current.Details, details);
        return await Save(finished with { Details = merged }, cancellationToken);
    }

    public async Task<Operation> Fail(Guid id, string error, CancellationToken cancellationToken)
    {
        var current = await Load(id, cancellationToken);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var sanitizedError = _sanitizer.SanitizeText(error);
        var failed = Transition(current, OperationStatus.Failed, now) with { FinishedAt = now, Error = sanitizedError };
        return await Save(failed, cancellationToken);
    }

    public async Task<Operation> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var current = await Load(id, cancellationToken);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var cancelled = Transition(current, OperationStatus.Cancelled, now) with { FinishedAt = now };
        return await Save(cancelled, cancellationToken);
    }

    public Task<Operation?> Find(Guid id, CancellationToken cancellationToken)
    {
        return _repository.Find(id, cancellationToken);
    }

    public Task<OperationPage> List(OperationQuery query, CancellationToken cancellationToken)
    {
        var normalized = query.Normalized();
        return _repository.Query(normalized, cancellationToken);
    }

    public async Task Retry(Guid id, CancellationToken cancellationToken)
    {
        var operation = await Load(id, cancellationToken);
        var isRetryableState = operation.Status is OperationStatus.Failed or OperationStatus.Cancelled;
        if (!operation.Retryable || !isRetryableState)
        {
            throw new OperationException(OperationFailure.NotRetryable, "this operation cannot be retried.");
        }

        if (!_retryHandlers.TryGetValue(operation.Kind, out var handler))
        {
            throw new OperationException(OperationFailure.NoRetryHandler, $"no retry handler is registered for operations of kind '{operation.Kind}'.");
        }

        await handler.Retry(operation, cancellationToken);
    }

    private static Dictionary<string, IOperationRetryHandler> IndexHandlers(IEnumerable<IOperationRetryHandler> handlers)
    {
        var indexed = new Dictionary<string, IOperationRetryHandler>(StringComparer.OrdinalIgnoreCase);
        foreach (var handler in handlers)
        {
            indexed[handler.Kind] = handler;
        }

        return indexed;
    }

    private static Operation Transition(Operation current, OperationStatus target, DateTime now)
    {
        if (!OperationStateMachine.CanTransition(current.Status, target))
        {
            throw new OperationException(OperationFailure.InvalidTransition, $"an operation cannot move from {current.Status} to {target}.");
        }

        return current with { Status = target, UpdatedAt = now };
    }

    private Dictionary<string, string> MergeDetails(IReadOnlyDictionary<string, string> existing, IReadOnlyDictionary<string, string>? additional)
    {
        var merged = new Dictionary<string, string>(existing, StringComparer.Ordinal);
        var sanitized = _sanitizer.SanitizeDetails(additional);
        foreach (var entry in sanitized)
        {
            merged[entry.Key] = entry.Value;
        }

        return merged;
    }

    private async Task<Operation> Load(Guid id, CancellationToken cancellationToken)
    {
        var operation = await _repository.Find(id, cancellationToken);
        if (operation is null)
        {
            throw new OperationException(OperationFailure.NotFound, "the operation was not found.");
        }

        return operation;
    }

    private async Task<Operation> Save(Operation operation, CancellationToken cancellationToken)
    {
        await _repository.Update(operation, cancellationToken);
        await _notifier.OperationChanged(operation, cancellationToken);
        return operation;
    }
}
