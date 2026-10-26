namespace DTech.Core.Files.Search;

public readonly record struct FileSearchLimits(int MaxDepth, int MaxResults, TimeSpan TimeLimit)
{
    public const int DefaultMaxDepth = 8;
    public const int UpperMaxDepth = 64;
    public const int DefaultMaxResults = 500;
    public const int UpperMaxResults = 5000;
    public const int DefaultTimeLimitSeconds = 20;
    public const int UpperTimeLimitSeconds = 300;
    public const int MaxReportedWarnings = 50;
}
