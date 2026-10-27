namespace DTech.Core.Transfers;

public sealed record TransferFailure(string Error, bool Retryable, TransferProgress Progress)
{
    public long PartialBytes { get; init; }
}
