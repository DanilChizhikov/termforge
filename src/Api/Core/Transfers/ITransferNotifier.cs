namespace DTech.Core.Transfers;

public interface ITransferNotifier
{
    Task TransferChanged(TransferSnapshot snapshot, CancellationToken cancellationToken);

    Task TransferRemoved(Guid id, CancellationToken cancellationToken);
}
