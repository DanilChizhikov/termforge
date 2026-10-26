using DTech.Core.Operations;

namespace DTech.Core.Transfers.Execution;

public sealed record TransferOutcome(OperationStatus Status)
{
    public static readonly TransferOutcome Succeeded = new(OperationStatus.Succeeded);
    public static readonly TransferOutcome Cancelled = new(OperationStatus.Cancelled);

    public TransferFailure? Failure { get; init; }

    public string? PartialPath { get; init; }

    public static TransferOutcome Failed(TransferFailure failure, string? partialPath)
    {
        return new TransferOutcome(OperationStatus.Failed) { Failure = failure, PartialPath = partialPath };
    }
}
