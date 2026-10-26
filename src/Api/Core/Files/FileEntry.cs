namespace DTech.Core.Files;

public sealed record FileEntry(string Name, string Path, FileEntryKind Kind)
{
    public long Size { get; init; }

    public DateTime? ModifiedUtc { get; init; }

    public string? Permissions { get; init; }

    public FileEntryKind? LinkTargetKind { get; init; }

    public bool IsBrokenLink { get; init; }
}
