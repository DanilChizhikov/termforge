namespace DTech.Core.Transfers.Execution;

public readonly record struct TransferRate(double BytesPerSecond, double? RemainingSeconds);
