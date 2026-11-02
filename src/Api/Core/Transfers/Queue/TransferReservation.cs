namespace DTech.Core.Transfers.Queue;

public sealed class TransferReservation(TransferQueue queue, IReadOnlyList<TransferTarget> targets) : IDisposable
{
    internal IReadOnlyList<TransferTarget> Targets { get; } = targets;

    public TransferAdmission Commit(IReadOnlyList<TransferJob> jobs)
    {
        return queue.Commit(this, jobs);
    }

    public void Dispose()
    {
        queue.Release(this);
    }
}
