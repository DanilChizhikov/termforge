using DTech.Core.Operations;

namespace DTech.Core.Transfers;

public sealed record TransferSnapshot
{
    public required Guid Id { get; init; }

    public required TransferRequest Request { get; init; }

    public required string TargetPath { get; init; }

    public required OperationStatus Status { get; init; }

    public required TransferProgress Progress { get; init; }

    public string? Error { get; init; }

    public bool Retryable { get; init; }

    public long PartialBytes { get; init; }
}
