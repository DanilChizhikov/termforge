using DTech.Core.Files;

namespace DTech.Entrypoint.Files;

public sealed record FileEntryResponse(
    string Name,
    string Path,
    string Kind,
    long Size,
    DateTime? ModifiedUtc,
    string? Permissions,
    string? LinkTargetKind,
    bool IsBrokenLink)
{
    public static FileEntryResponse From(FileEntry entry)
    {
        var kind = entry.Kind.ToString();
        var linkTargetKind = entry.LinkTargetKind?.ToString();
        return new FileEntryResponse(entry.Name, entry.Path, kind, entry.Size, entry.ModifiedUtc, entry.Permissions, linkTargetKind, entry.IsBrokenLink);
    }
}
