namespace DTech.Core.Files.Search;

public sealed class FileSearchTally(FileSearchMethod method)
{
    public int MatchCount { get; private set; }

    public int WarningCount { get; private set; }

    public bool DepthLimitReached { get; private set; }

    public void RecordMatch()
    {
        MatchCount++;
    }

    public bool RecordWarning()
    {
        WarningCount++;
        return WarningCount <= FileSearchLimits.MaxReportedWarnings;
    }

    public void RecordDepthLimit()
    {
        DepthLimitReached = true;
    }

    public FileSearchCompleted Complete(FileSearchOutcome outcome)
    {
        var summary = CreateSummary(outcome);
        return new FileSearchCompleted(summary);
    }

    public FileSearchCompleted Fail(string message)
    {
        var summary = CreateSummary(FileSearchOutcome.Failed) with { Message = message };
        return new FileSearchCompleted(summary);
    }

    private FileSearchSummary CreateSummary(FileSearchOutcome outcome)
    {
        return new FileSearchSummary(outcome, method, MatchCount, WarningCount)
        {
            DepthLimitReached = DepthLimitReached,
        };
    }
}
