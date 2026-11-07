using DTech.Core.Transfers;

namespace DTech.Entrypoint.Transfers;

public sealed record TransferResponse
{
    public required Guid Id { get; init; }

    public required Guid ConnectionId { get; init; }

    public Guid? TargetConnectionId { get; init; }

    public string? TargetServer { get; init; }

    public required string Server { get; init; }

    public required string Direction { get; init; }

    public required string SourcePath { get; init; }

    public required string TargetPath { get; init; }

    public required string TargetDirectory { get; init; }

    public bool HasPartial { get; init; }

    public required string Status { get; init; }

    public required long TransferredBytes { get; init; }

    public required long TotalBytes { get; init; }

    public required int CompletedFiles { get; init; }

    public required int TotalFiles { get; init; }

    public required double BytesPerSecond { get; init; }

    public double? RemainingSeconds { get; init; }

    public string? Error { get; init; }

    public bool Retryable { get; init; }

    public long PartialBytes { get; init; }

    public static TransferResponse From(TransferSnapshot snapshot)
    {
        var progress = snapshot.Progress;
        return new TransferResponse
        {
            Id = snapshot.Id,
            ConnectionId = snapshot.Request.ConnectionId,
            TargetConnectionId = snapshot.Request.TargetConnectionId,
            TargetServer = snapshot.Request.TargetServerName,
            Server = snapshot.Request.ServerName,
            Direction = snapshot.Request.Direction == TransferDirection.RemoteToRemote ? "remoteToRemote" : snapshot.Request.Direction.ToString().ToLowerInvariant(),
            SourcePath = snapshot.Request.SourcePath,
            TargetPath = snapshot.TargetPath,
            TargetDirectory = snapshot.Request.TargetDirectory,
            HasPartial = snapshot.HasPartial,
            Status = snapshot.Status.ToString().ToLowerInvariant(),
            TransferredBytes = progress.TransferredBytes,
            TotalBytes = progress.TotalBytes,
            CompletedFiles = progress.CompletedFiles,
            TotalFiles = progress.TotalFiles,
            BytesPerSecond = progress.BytesPerSecond,
            RemainingSeconds = progress.RemainingSeconds,
            Error = snapshot.Error,
            Retryable = snapshot.Retryable,
            PartialBytes = snapshot.PartialBytes,
        };
    }
}
