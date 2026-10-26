namespace DTech.Core.Transfers.Execution;

public sealed record TransferPartialIdentity
{
    public required TransferDirection Direction { get; init; }

    public required Guid ConnectionId { get; init; }

    public required string SourcePath { get; init; }

    public required string TargetPath { get; init; }

    public required long SourceSize { get; init; }

    public required DateTime SourceModifiedUtc { get; init; }
}
