using DTech.Core.Diagnostics;

namespace DTech.Core.Files.Search;

public static class RemoteFindOutputParser
{
    private const char RecordSeparator = '\0';
    private const char DirectoryMarker = 'd';
    private const char FileMarker = 'f';
    private const string ToolPrefix = "find:";
    private const string PermissionDeniedSuffix = ": Permission denied";
    private const int MaxMessageLength = 300;

    private static readonly char[] QuoteCharacters = ['\'', '"', '‘', '’', '`'];

    public static RemoteFindReport Parse(RemoteCommandResult result, int maxResults)
    {
        var matches = ParseMatches(result.StandardOutput);
        var warnings = ParseWarnings(result.StandardError);
        var outcome = DetermineOutcome(result, matches.Count, maxResults);
        var accepted = matches.Count > maxResults ? matches.GetRange(0, maxResults) : matches;
        if (outcome != FileSearchOutcome.Failed)
        {
            return new RemoteFindReport(accepted, warnings, outcome);
        }

        var failureMessage = DescribeFailure(result);
        return new RemoteFindReport(accepted, warnings, outcome) { FailureMessage = failureMessage };
    }

    private static FileSearchOutcome DetermineOutcome(RemoteCommandResult result, int matchCount, int maxResults)
    {
        if (result.ExitCode == RemoteFindCommand.TimeLimitExitCode)
        {
            return FileSearchOutcome.TimeLimitReached;
        }

        if (result.OutputTruncated || matchCount > maxResults)
        {
            return FileSearchOutcome.ResultLimitReached;
        }

        if (result.ExitCode == 0)
        {
            return FileSearchOutcome.Completed;
        }

        var reportedPermissionErrors = result.ExitCode == 1 && result.StandardError.Contains(PermissionDeniedSuffix, StringComparison.Ordinal);
        return reportedPermissionErrors ? FileSearchOutcome.Completed : FileSearchOutcome.Failed;
    }

    private static List<FileSearchMatch> ParseMatches(string output)
    {
        var matches = new List<FileSearchMatch>();
        var records = output.Split(RecordSeparator);
        var completeRecords = records.Length - 1;
        for (var i = 0; i < completeRecords; i++)
        {
            var match = ParseRecord(records[i]);
            if (match is not null)
            {
                matches.Add(match);
            }
        }

        return matches;
    }

    private static FileSearchMatch? ParseRecord(string record)
    {
        if (record.Length < 2 || record[1] != '/')
        {
            return null;
        }

        var marker = record[0];
        if (marker is not (DirectoryMarker or FileMarker))
        {
            return null;
        }

        var path = record[1..];
        var separatorIndex = path.LastIndexOf('/');
        var name = path[(separatorIndex + 1)..];
        if (name.Length == 0)
        {
            return null;
        }

        var parentPath = separatorIndex == 0 ? "/" : path[..separatorIndex];
        var kind = marker == DirectoryMarker ? FileEntryKind.Directory : FileEntryKind.File;
        var entry = new FileEntry(name, path, kind);
        return new FileSearchMatch(entry, parentPath);
    }

    private static List<FileSearchWarning> ParseWarnings(string standardError)
    {
        var warnings = new List<FileSearchWarning>();
        var lines = RemoteText.SplitLines(standardError);
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var warning = ParseWarning(line);
            warnings.Add(warning);
        }

        return warnings;
    }

    private static FileSearchWarning ParseWarning(string line)
    {
        var body = line.StartsWith(ToolPrefix, StringComparison.Ordinal) ? line[ToolPrefix.Length..].Trim() : line;
        if (!body.EndsWith(PermissionDeniedSuffix, StringComparison.Ordinal))
        {
            var message = RemoteText.Sanitize(body, MaxMessageLength);
            var failure = new FileError(FileErrorCode.Failed, message);
            return new FileSearchWarning(string.Empty, failure);
        }

        var rawPath = body[..^PermissionDeniedSuffix.Length].Trim().Trim(QuoteCharacters);
        var path = RemoteText.Sanitize(rawPath, MaxMessageLength);
        var denied = FileError.PermissionDenied(path);
        return new FileSearchWarning(path, denied);
    }

    private static string DescribeFailure(RemoteCommandResult result)
    {
        var detail = RemoteText.Sanitize(result.StandardError, MaxMessageLength);
        if (detail.Length == 0)
        {
            return $"Remote find exited with code {result.ExitCode}.";
        }

        return $"Remote find exited with code {result.ExitCode}: {detail}";
    }
}
