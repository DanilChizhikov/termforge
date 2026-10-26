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
        var blocks = long.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var available = long.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
        var mountPoint = match.Groups[4].Value;
        if (blocks <= 0)
        {
            return MetricReading<DiskUsage>.Unavailable($"df reported no capacity for {mountPoint}.");
        }

        var usage = new DiskUsage(available * KibiByte, blocks * KibiByte, mountPoint);
        return MetricReading<DiskUsage>.Available(usage);
    }

    [GeneratedRegex(@"^\S.*?\s+(\d+)\s+(\d+)\s+(\d+)\s+\d+%\s+(/.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex DfLine();
}
