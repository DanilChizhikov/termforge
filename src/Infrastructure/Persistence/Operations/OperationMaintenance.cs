using DTech.Core.Operations;
using DTech.Core.Transfers;
using DTech.Persistence.Database;
using Microsoft.EntityFrameworkCore;

namespace DTech.Persistence.Operations;

public sealed class OperationMaintenance(TermForgeDbContext context) : IOperationMaintenance
{
    private const int RecoveryBatchSize = 200;

    public Task CheckpointAsync(OperationCheckpoint checkpoint, CancellationToken cancellationToken)
    {
        return context.Operations.Where(operation => operation.Id == checkpoint.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(operation => operation.RecoveryData, checkpoint.RecoveryData)
                .SetProperty(operation => operation.HasPendingCleanup, checkpoint.HasPendingCleanup), cancellationToken);
    }

    public async Task RecoverAsync(OperationRecoveryPolicy policy, CancellationToken cancellationToken)
    {
        var retryableKinds = new HashSet<string>(policy.RetryableKinds, StringComparer.OrdinalIgnoreCase);
        while (true)
        {
            var interrupted = await context.Operations.AsNoTracking()
                .Where(operation => operation.Status == OperationStatus.Queued || operation.Status == OperationStatus.Running)
                .OrderBy(operation => operation.Id)
                .Take(RecoveryBatchSize)
                .ToListAsync(cancellationToken);
            if (interrupted.Count == 0)
            {
                return;
            }

            for (var i = 0; i < interrupted.Count; i++)
            {
                var operation = interrupted[i];
                var retryable = retryableKinds.Contains(operation.Kind);
                if (operation.Kind is TransferOperationKinds.Upload or TransferOperationKinds.Download or TransferOperationKinds.RemoteToRemote)
                {
                    retryable &= TransferOperationDetails.TryRead(operation) is not null;
                }

                var recovered = operation with
                {
                    Status = OperationStatus.Failed,
                    Error = "The operation was interrupted when the application stopped.",
                    UpdatedAt = policy.Now,
                    FinishedAt = policy.Now,
                    Retryable = retryable,
                };
                context.Operations.Update(recovered);
            }

            await context.SaveChangesAsync(cancellationToken);
            context.ChangeTracker.Clear();
        }
    }

    public async Task<IReadOnlyList<Operation>> ListPendingCleanupAsync(CancellationToken cancellationToken)
    {
        return await context.Operations.AsNoTracking()
            .Where(operation => operation.HasPendingCleanup
                && (operation.Status == OperationStatus.Succeeded || operation.Status == OperationStatus.Failed || operation.Status == OperationStatus.Cancelled))
            .OrderBy(operation => operation.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task PruneAsync(OperationRetentionPolicy policy, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(policy.MaximumCompleted, 1);
        var eligible = context.Operations.Where(operation => !operation.HasPendingCleanup
            && (operation.Status == OperationStatus.Succeeded || operation.Status == OperationStatus.Failed || operation.Status == OperationStatus.Cancelled));
        await eligible.Where(operation => (operation.FinishedAt ?? operation.CreatedAt) < policy.OlderThan)
            .ExecuteDeleteAsync(cancellationToken);
        var overflow = eligible.OrderByDescending(operation => operation.FinishedAt ?? operation.CreatedAt)
            .ThenBy(operation => operation.Id)
            .Skip(policy.MaximumCompleted)
            .Select(operation => operation.Id);
        await context.Operations.Where(operation => overflow.Contains(operation.Id)).ExecuteDeleteAsync(cancellationToken);
    }
}
