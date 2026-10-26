namespace DTech.Core.Transfers.Execution;

public sealed record TransferItem(string SourcePath, string TargetPath, bool IsDirectory)
{
    public long Size { get; init; }

    public DateTime ModifiedUtc { get; init; }

    public string TargetName { get; init; } = string.Empty;
}
