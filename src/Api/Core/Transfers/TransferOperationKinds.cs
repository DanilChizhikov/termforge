namespace DTech.Core.Transfers;

public static class TransferOperationKinds
{
    public const string Upload = "transfer.upload";
    public const string Download = "transfer.download";

    public static string For(TransferDirection direction)
    {
        return direction == TransferDirection.Upload ? Upload : Download;
    }
}
