using System.Globalization;
using System.Text.RegularExpressions;

namespace DTech.Core.Monitoring.Parsing;

public static partial class DiskUsageParser
{
    private const long KibiByte = 1024;

    public static MetricReading<DiskUsage> ParseDf(string df)
    {
        var lines = df.Split('\n');
        for (var i = lines.Length - 1; i >= 0; i--)
        {
            var match = DfLine().Match(lines[i].Trim());
            if (match.Success)
            {
                return ToReading(match);
            }
        }

        return MetricReading<DiskUsage>.Unavailable("df did not report the root filesystem.");
    }

    private static MetricReading<DiskUsage> ToReading(Match match)
    {
        var mountPoint = match.Groups[4].Value;
        if (!long.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var blocks) ||
            !long.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var used) ||
            !long.TryParse(match.Groups[3].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var available) ||
            blocks <= 0 || blocks > long.MaxValue / KibiByte || available > blocks || used > blocks)
        {
            return MetricReading<DiskUsage>.Unavailable($"df reported invalid capacity for {mountPoint}.");
        }

        var usage = new DiskUsage(available * KibiByte, blocks * KibiByte, mountPoint);
        return MetricReading<DiskUsage>.Available(usage);
    }

    [GeneratedRegex(@"^\S.*?\s+(\d+)\s+(\d+)\s+(\d+)\s+\d+%\s+(/.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex DfLine();
}
