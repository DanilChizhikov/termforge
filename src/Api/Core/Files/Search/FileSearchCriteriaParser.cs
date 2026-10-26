namespace DTech.Core.Files.Search;

public static class FileSearchCriteriaParser
{
    public static FileResult<FileSearchCriteria> Parse(FileSearchRequest request)
    {
        var pattern = FileNamePattern.Create(request.Pattern);
        if (!pattern.IsSuccess)
        {
            return FileResult<FileSearchCriteria>.Failure(pattern.Error);
        }

        var limits = ParseLimits(request);
        if (!limits.IsSuccess)
        {
            return FileResult<FileSearchCriteria>.Failure(limits.Error);
        }

        var criteria = new FileSearchCriteria(pattern.Value, limits.Value);
        return FileResult<FileSearchCriteria>.Success(criteria);
    }

    private static FileResult<FileSearchLimits> ParseLimits(FileSearchRequest request)
    {
        var maxDepth = request.MaxDepth ?? FileSearchLimits.DefaultMaxDepth;
        if (maxDepth < 1 || maxDepth > FileSearchLimits.UpperMaxDepth)
        {
            return Reject($"Maximum depth must be between 1 and {FileSearchLimits.UpperMaxDepth}.");
        }

        var maxResults = request.MaxResults ?? FileSearchLimits.DefaultMaxResults;
        if (maxResults < 1 || maxResults > FileSearchLimits.UpperMaxResults)
        {
            return Reject($"Result limit must be between 1 and {FileSearchLimits.UpperMaxResults}.");
        }

        var timeLimitSeconds = request.TimeLimitSeconds ?? FileSearchLimits.DefaultTimeLimitSeconds;
        if (timeLimitSeconds < 1 || timeLimitSeconds > FileSearchLimits.UpperTimeLimitSeconds)
        {
            return Reject($"Time limit must be between 1 and {FileSearchLimits.UpperTimeLimitSeconds} seconds.");
        }

        var timeLimit = TimeSpan.FromSeconds(timeLimitSeconds);
        var limits = new FileSearchLimits(maxDepth, maxResults, timeLimit);
        return FileResult<FileSearchLimits>.Success(limits);
    }

    private static FileResult<FileSearchLimits> Reject(string message)
    {
        var error = FileError.Invalid(message);
        return FileResult<FileSearchLimits>.Failure(error);
    }
}
