namespace DTech.Core.Transfers.Queue;

public enum TransferCancelOutcome
{
    NotFound,
    AlreadyFinished,
    CancelledWhileQueued,
    CancellationRequested,
}
