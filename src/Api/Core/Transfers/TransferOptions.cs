namespace DTech.Core.Transfers;

public sealed class TransferOptions
{
    public const string SectionName = "Transfers";
    public const int MinParallelTransfers = 1;
    public const int MaxParallelTransfersLimit = 8;

    public int MaxParallelTransfers { get; set; } = 2;

    public int ProgressIntervalMilliseconds { get; set; } = 250;

    public int ShutdownTimeoutSeconds { get; set; } = 10;

    public int EffectiveParallelism => Math.Clamp(MaxParallelTransfers, MinParallelTransfers, MaxParallelTransfersLimit);

    public TimeSpan ProgressInterval => TimeSpan.FromMilliseconds(Math.Max(50, ProgressIntervalMilliseconds));

    public TimeSpan ShutdownTimeout => TimeSpan.FromSeconds(Math.Max(1, ShutdownTimeoutSeconds));
}
