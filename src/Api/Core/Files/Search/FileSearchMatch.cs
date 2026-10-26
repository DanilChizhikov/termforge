namespace DTech.Core.Files.Search;

public sealed record FileSearchMatch(FileEntry Entry, string ParentPath) : FileSearchEvent;
