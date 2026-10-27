using DTech.Core.Operations;
using DTech.Core.Transfers;

namespace DTech.Entrypoint.Lifecycle;

internal sealed class ActiveTransferWatcher(TransferControl control, TimeProvider timeProvider)
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

    public bool HasActiveTransfers()
    {
        var transfers = control.List();
        for (var i = 0; i < transfers.Count; i++)
        {
            if (!OperationStateMachine.IsTerminal(transfers[i].Status))
            {
                return true;
            }
        }

        return false;
    }

    public async Task WaitUntilIdleAsync(CancellationToken cancellationToken)
    {
        while (HasActiveTransfers())
        {
            await Task.Delay(PollInterval, timeProvider, cancellationToken);
        }
    }
}
