namespace DTech.Core.Transfers;

public static class TransferOperationKinds
{
    public const string Upload = "transfer.upload";
    public const string Download = "transfer.download";
    public const string RemoteToRemote = "transfer.remoteToRemote";

    public static string For(TransferDirection direction)
    {
        return direction switch
        {
            TransferDirection.Upload => Upload,
            TransferDirection.Download => Download,
            TransferDirection.RemoteToRemote => RemoteToRemote,
            _ => throw new ArgumentOutOfRangeException(nameof(direction)),
        };
    }
}
