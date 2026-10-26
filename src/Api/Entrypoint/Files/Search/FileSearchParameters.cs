using DTech.Core.Files.Search;

namespace DTech.Entrypoint.Files.Search;

public sealed class FileSearchParameters
{
    public const string RemoteFindMethod = "find";

    public string? Path { get; init; }

    public string? Pattern { get; init; }

    public int? MaxDepth { get; init; }

    public int? MaxResults { get; init; }

    public int? TimeLimitSeconds { get; init; }

    public string? Method { get; init; }

    public bool UsesRemoteFind => string.Equals(Method, RemoteFindMethod, StringComparison.OrdinalIgnoreCase);

    public FileSearchRequest ToRequest()
    {
        return new FileSearchRequest(Path, Pattern)
        {
            MaxDepth = MaxDepth,
            MaxResults = MaxResults,
            TimeLimitSeconds = TimeLimitSeconds,
        };
    }
}
