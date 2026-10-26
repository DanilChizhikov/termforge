using System.Globalization;
using System.Text.RegularExpressions;

namespace DTech.Core.Monitoring.Parsing;

public static partial class CpuUsageParser
{
    private const int MinimumCounters = 4;
    private const int IdleIndex = 3;
    private const int IoWaitIndex = 4;
    private const int CountedFields = 8;

    public static MetricReading<double> ParseProcStat(string first, string second)
    {
        var before = ReadCounters(first);
        var after = ReadCounters(second);
        if (before is null || after is null)
        {
            return MetricReading<double>.Unavailable("/proc/stat could not be read or has an unexpected format.");
        }

        var totalDelta = Total(after) - Total(before);
        var idleDelta = Idle(after) - Idle(before);
        if (totalDelta <= 0 || idleDelta < 0)
        {
            return MetricReading<double>.Unavailable("CPU counters did not advance between the two samples.");
        }

        var busy = 100d * (1d - (double)idleDelta / totalDelta);
        return MetricReading<double>.Available(Round(busy));
    }

    public static MetricReading<double> ParseTop(string line)
    {
        var match = IdlePercentage().Match(line);
        if (!match.Success)
        {
            return MetricReading<double>.Unavailable("top did not report CPU usage.");
        }

        var text = match.Groups[1].Value.Replace(',', '.');
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var idle))
        {
            return MetricReading<double>.Unavailable("top reported CPU usage in an unreadable format.");
        }

        return MetricReading<double>.Available(Round(100d - idle));
    }

    private static long[]? ReadCounters(string line)
    {
        var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (fields.Length < MinimumCounters + 1 || fields[0] != "cpu")
        {
            return null;
        }

        var count = Math.Min(fields.Length - 1, CountedFields);
        var counters = new long[CountedFields];
        for (var i = 0; i < count; i++)
        {
            if (!long.TryParse(fields[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out counters[i]))
            {
                return null;
            }
        }

        return counters;
    }

    private static long Total(long[] counters)
    {
        long total = 0;
        for (var i = 0; i < counters.Length; i++)
        {
            total += counters[i];
        }

        return total;
    }

    private static long Idle(long[] counters)
    {
        return counters[IdleIndex] + counters[IoWaitIndex];
    }

    private static double Round(double percent)
    {
        var clamped = Math.Clamp(percent, 0d, 100d);
        return Math.Round(clamped, 1);
    }

    [GeneratedRegex(@"([\d.,]+)%\s*idle", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IdlePercentage();
}
