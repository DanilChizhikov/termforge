using DTech.Core.Files.Search;

namespace DTech.Entrypoint.Files.Search;

public sealed record FileSearchEventResponse(string Type)
{
    public const string MatchType = "match";
    public const string WarningType = "warning";
    public const string NoticeType = "notice";
    public const string CompletedType = "completed";

    public FileEntryResponse? Entry { get; init; }

    public string? ParentPath { get; init; }

    public string? Path { get; init; }

    public string? Code { get; init; }

    public string? Message { get; init; }

    public FileSearchSummaryResponse? Summary { get; init; }

    public static FileSearchEventResponse From(FileSearchEvent searchEvent)
    {
        return searchEvent switch
        {
            FileSearchMatch match => FromMatch(match),
            FileSearchWarning warning => FromWarning(warning),
            FileSearchNotice notice => new FileSearchEventResponse(NoticeType) { Message = notice.Message },
            FileSearchCompleted completed => FromCompleted(completed),
            _ => throw new ArgumentOutOfRangeException(nameof(searchEvent), searchEvent.GetType().Name, "Unknown search event."),
        };
    }

    private static FileSearchEventResponse FromMatch(FileSearchMatch match)
    {
        var entry = FileEntryResponse.From(match.Entry);
        return new FileSearchEventResponse(MatchType) { Entry = entry, ParentPath = match.ParentPath };
    }

    private static FileSearchEventResponse FromWarning(FileSearchWarning warning)
    {
        var code = warning.Error.Code.ToString();
        return new FileSearchEventResponse(WarningType) { Path = warning.Path, Code = code, Message = warning.Error.Message };
    }

    private static FileSearchEventResponse FromCompleted(FileSearchCompleted completed)
    {
        var summary = FileSearchSummaryResponse.From(completed.Summary);
        return new FileSearchEventResponse(CompletedType) { Summary = summary };
    }
}
