namespace DTech.Core.Transfers;

public sealed record TransferBatchRequest
{
    public required Guid ConnectionId { get; init; }

    public required string ServerName { get; init; }

    public required TransferDirection Direction { get; init; }

    public required IReadOnlyList<string> SourcePaths { get; init; }

    public required string TargetDirectory { get; init; }

    public bool Overwrite { get; init; }
}
