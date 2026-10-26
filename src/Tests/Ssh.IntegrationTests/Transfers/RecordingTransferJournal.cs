using System.Collections.Concurrent;
using DTech.Core.Operations;
using DTech.Core.Transfers;

namespace DTech.Ssh.IntegrationTests.Transfers;

internal sealed class RecordingTransferJournal : ITransferJournal
{
    private readonly ConcurrentDictionary<Guid, Operation> _operations = new();

    public Operation Get(Guid id)
    {
        return _operations[id];
    }

    public Task<Guid> CreateAsync(TransferRequest request, string targetPath, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var operation = new Operation
        {
            Id = Guid.NewGuid(),
            Kind = TransferOperationKinds.For(request.Direction),
            Server = request.ServerName,
            Status = OperationStatus.Queued,
            CreatedAt = now,
            UpdatedAt = now,
            Details = TransferOperationDetails.Describe(request, targetPath),
            Retryable = true,
        };
        _operations[operation.Id] = operation;
        return Task.FromResult(operation.Id);
    }

    public Task StartAsync(Guid id, CancellationToken cancellationToken)
    {
        return Move(id, OperationStatus.Running, null);
    }

    public Task SucceedAsync(Guid id, TransferProgress progress, CancellationToken cancellationToken)
    {
        return Move(id, OperationStatus.Succeeded, null);
    }

    public Task FailAsync(Guid id, TransferFailure failure, CancellationToken cancellationToken)
    {
        return Move(id, OperationStatus.Failed, failure);
    }

    public Task CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        return Move(id, OperationStatus.Cancelled, null);
    }

    private Task Move(Guid id, OperationStatus status, TransferFailure? failure)
    {
        var current = _operations[id];
        if (!OperationStateMachine.CanTransition(current.Status, status))
        {
            throw new InvalidOperationException($"Invalid journal transition {current.Status} -> {status}");
        }

        _operations[id] = current with
        {
            Status = status,
            Error = failure?.Error,
            Retryable = failure?.Retryable ?? current.Retryable,
        };
        return Task.CompletedTask;
    }
}
