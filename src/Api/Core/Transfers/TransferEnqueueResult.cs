using DTech.Core.Files;

namespace DTech.Core.Transfers;

public sealed record TransferEnqueueResult
{
    private static readonly TransferSnapshot[] NoTransfers = [];
    private static readonly string[] NoConflicts = [];

    public IReadOnlyList<TransferSnapshot> Transfers { get; init; } = NoTransfers;

    public IReadOnlyList<string> Conflicts { get; init; } = NoConflicts;

    public FileError? Error { get; init; }

    public bool IsSuccess => Error is null && Conflicts.Count == 0;

    public static TransferEnqueueResult Queued(IReadOnlyList<TransferSnapshot> transfers)
    {
        return new TransferEnqueueResult { Transfers = transfers };
    }

    public static TransferEnqueueResult Conflicted(IReadOnlyList<string> conflicts)
    {
        return new TransferEnqueueResult { Conflicts = conflicts };
    }

    public static TransferEnqueueResult Failed(FileError error)
    {
        return new TransferEnqueueResult { Error = error };
    }
}
