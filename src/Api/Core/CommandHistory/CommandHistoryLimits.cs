namespace DTech.Core.CommandHistory;

public static class CommandHistoryLimits
{
    public const int MaxCommandLength = 2000;
    public const int MaxUnpinnedEntriesPerConnection = 1000;
    public const int DefaultPageSize = 200;
    public const int MaxPageSize = 500;
    public const int MaxSearchLength = 200;
    public const int MaxExclusionPatterns = 50;
    public const int MaxExclusionPatternLength = 300;
}
