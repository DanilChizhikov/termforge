namespace DTech.Core.Files.Search;

public sealed record FileSearchRequest(string? RootPath, string? Pattern)
{
    public int? MaxDepth { get; init; }

    public int? MaxResults { get; init; }

    public int? TimeLimitSeconds { get; init; }
}
