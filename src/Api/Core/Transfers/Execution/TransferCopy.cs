namespace DTech.Core.Transfers.Execution;

public sealed record TransferCopy(TransferItem Item, string PartialPath, long Offset)
{
    public IProgress<long>? Progress { get; init; }
}
