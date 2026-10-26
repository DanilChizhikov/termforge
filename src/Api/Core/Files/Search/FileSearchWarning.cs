namespace DTech.Core.Files.Search;

public sealed record FileSearchWarning(string Path, FileError Error) : FileSearchEvent;
