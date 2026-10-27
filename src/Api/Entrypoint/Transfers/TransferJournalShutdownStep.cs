using DTech.Entrypoint.Lifecycle;

namespace DTech.Entrypoint.Transfers;

internal sealed class TransferJournalShutdownStep(TransferQueueHostedService queueService) : IShutdownStep
{
    public ShutdownStage Stage => ShutdownStage.FlushJournal;

    public Task RunAsync(CancellationToken cancellationToken)
    {
        return queueService.WaitForWorkersAsync(cancellationToken);
    }
}
