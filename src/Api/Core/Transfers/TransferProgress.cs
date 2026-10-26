namespace DTech.Core.Transfers;

public readonly record struct TransferProgress(
    long TransferredBytes,
    long TotalBytes,
    int CompletedFiles,
    int TotalFiles,
    double BytesPerSecond,
    double? RemainingSeconds);
