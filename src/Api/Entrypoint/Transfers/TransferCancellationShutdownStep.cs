using DTech.Core.Transfers;
using DTech.Entrypoint.Lifecycle;

namespace DTech.Entrypoint.Transfers;

internal sealed class TransferCancellationShutdownStep(TransferControl control) : IShutdownStep
{
    public ShutdownStage Stage => ShutdownStage.CancelTransfers;

    public Task RunAsync(CancellationToken cancellationToken)
    {
        return control.CancelAllAsync();
    }
}
