using System.Globalization;
using DTech.Core.Operations;

namespace DTech.Core.Transfers;

public static class TransferOperationDetails
{
    public const string ConnectionIdKey = "connectionId";
    public const string DirectionKey = "direction";
    public const string SourceKey = "source";
    public const string TargetDirectoryKey = "targetDirectory";
    public const string TargetKey = "target";
    public const string OverwriteKey = "overwrite";
    public const string TransferredBytesKey = "transferredBytes";
    public const string TotalBytesKey = "totalBytes";
    public const string FilesKey = "files";
    public const string PartialBytesKey = "partialBytes";

    public static Dictionary<string, string> Describe(TransferRequest request, string targetPath)
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ConnectionIdKey] = request.ConnectionId.ToString(),
            [DirectionKey] = request.Direction.ToString(),
            [SourceKey] = request.SourcePath,
            [TargetDirectoryKey] = request.TargetDirectory,
            [TargetKey] = targetPath,
            [OverwriteKey] = request.Overwrite ? "true" : "false",
        };
    }

    public static Dictionary<string, string> Summarize(TransferProgress progress)
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [TransferredBytesKey] = Format(progress.TransferredBytes),
            [TotalBytesKey] = Format(progress.TotalBytes),
            [FilesKey] = $"{progress.CompletedFiles}/{progress.TotalFiles}",
        };
    }

    public static Dictionary<string, string> Summarize(TransferFailure failure)
    {
        var summary = Summarize(failure.Progress);
        summary[PartialBytesKey] = Format(failure.PartialBytes);
        return summary;
    }

    public static TransferRequest? TryRead(Operation operation)
    {
        var details = operation.Details;
        var hasValues = details.TryGetValue(ConnectionIdKey, out var connectionText)
            & details.TryGetValue(DirectionKey, out var directionText)
            & details.TryGetValue(SourceKey, out var source)
            & details.TryGetValue(TargetDirectoryKey, out var targetDirectory);
        if (!hasValues || !Guid.TryParse(connectionText, out var connectionId) || !Enum.TryParse<TransferDirection>(directionText, out var direction))
        {
            return null;
        }

        details.TryGetValue(OverwriteKey, out var overwriteText);
        return new TransferRequest
        {
            ConnectionId = connectionId,
            ServerName = operation.Server ?? string.Empty,
            Direction = direction,
            SourcePath = source!,
            TargetDirectory = targetDirectory!,
            Overwrite = overwriteText == "true",
        };
    }

    private static string Format(long value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }
}
