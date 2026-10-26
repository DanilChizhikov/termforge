namespace DTech.Core.Files.Search;

public sealed record FileSearchCompleted(FileSearchSummary Summary) : FileSearchEvent;
