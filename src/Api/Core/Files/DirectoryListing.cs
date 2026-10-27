namespace DTech.Core.Files;

public sealed record DirectoryListing(string Path, string? ParentPath, IReadOnlyList<FileEntry> Entries);
