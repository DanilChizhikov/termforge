namespace DTech.Entrypoint.Lifecycle;

internal sealed record ExitRequest(bool WaitForTransfers)
{
    public ExitTransferPolicy Policy => WaitForTransfers ? ExitTransferPolicy.WaitForTransfers : ExitTransferPolicy.CancelTransfers;
}
