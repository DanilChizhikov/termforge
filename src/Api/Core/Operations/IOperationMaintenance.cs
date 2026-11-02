namespace DTech.Core.Operations;

public interface IOperationMaintenance
{
    Task CheckpointAsync(OperationCheckpoint checkpoint, CancellationToken cancellationToken);

    Task RecoverAsync(OperationRecoveryPolicy policy, CancellationToken cancellationToken);

    Task<IReadOnlyList<Operation>> ListPendingCleanupAsync(CancellationToken cancellationToken);

    Task PruneAsync(OperationRetentionPolicy policy, CancellationToken cancellationToken);
}
