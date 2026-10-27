using DTech.Core.Files.Search;

namespace DTech.Entrypoint.Files.Search;

public sealed record FileSearchSummaryResponse(
    string Outcome,
    string Method,
    int MatchCount,
    int WarningCount,
    bool DepthLimitReached,
    bool IsPartial,
    string? Message)
{
    public static FileSearchSummaryResponse From(FileSearchSummary summary)
    {
        var outcome = summary.Outcome.ToString();
        var method = summary.Method.ToString();
        return new FileSearchSummaryResponse(outcome, method, summary.MatchCount, summary.WarningCount, summary.DepthLimitReached, summary.IsPartial, summary.Message);
    }
}
