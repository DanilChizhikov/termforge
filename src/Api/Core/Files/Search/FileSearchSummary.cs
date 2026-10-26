namespace DTech.Core.Files.Search;

public sealed record FileSearchSummary(FileSearchOutcome Outcome, FileSearchMethod Method, int MatchCount, int WarningCount)
{
    public bool DepthLimitReached { get; init; }

    public string? Message { get; init; }

    public bool IsPartial => Outcome != FileSearchOutcome.Completed || DepthLimitReached || WarningCount > 0;
}
