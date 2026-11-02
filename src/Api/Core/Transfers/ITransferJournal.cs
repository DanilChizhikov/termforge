using DTech.Core.Transfers.Queue;

namespace DTech.Core.Transfers;

public interface ITransferJournal
{
    Task<Guid> CreateAsync(TransferRequest request, string targetPath, CancellationToken cancellationToken);

    Task StartAsync(Guid id, CancellationToken cancellationToken);

    Task SucceedAsync(Guid id, TransferProgress progress, CancellationToken cancellationToken);

    Task FailAsync(Guid id, TransferFailure failure, CancellationToken cancellationToken);

    Task CancelAsync(Guid id, CancellationToken cancellationToken);

    Task CheckpointAsync(TransferJob job, CancellationToken cancellationToken);

    Task ForgetRecoveryAsync(Guid id, CancellationToken cancellationToken);
}
