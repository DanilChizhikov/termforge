using System.Globalization;
using System.Text.RegularExpressions;

namespace DTech.Core.Monitoring.Parsing;

public static partial class MemoryUsageParser
{
    private const long KibiByte = 1024;

    public static MetricReading<MemoryUsage> ParseMeminfo(string meminfo)
    {
        var values = ReadMeminfo(meminfo);
        if (!values.TryGetValue("MemTotal", out var total) || total <= 0)
        {
            return MetricReading<MemoryUsage>.Unavailable("/proc/meminfo does not report MemTotal.");
        }

        var available = FindAvailable(values);
        if (available is null)
        {
            return MetricReading<MemoryUsage>.Unavailable("/proc/meminfo reports neither MemAvailable nor MemFree.");
        }

        var totalBytes = total * KibiByte;
        var usedBytes = Math.Clamp(totalBytes - available.Value * KibiByte, 0, totalBytes);
        return MetricReading<MemoryUsage>.Available(new MemoryUsage(usedBytes, totalBytes));
    }

    public static MetricReading<MemoryUsage> ParseVmStat(string memorySize, string vmStat)
    {
        if (!long.TryParse(memorySize.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var totalBytes) || totalBytes <= 0)
        {
            return MetricReading<MemoryUsage>.Unavailable("sysctl hw.memsize did not report the installed memory.");
        }

        var pageSizeMatch = PageSize().Match(vmStat);
        if (!pageSizeMatch.Success)
        {
            return MetricReading<MemoryUsage>.Unavailable("vm_stat did not report a page size.");
        }

        var pageSize = long.Parse(pageSizeMatch.Groups[1].Value, CultureInfo.InvariantCulture);
        var free = ReadPages(vmStat, "Pages free");
        if (free is null)
        {
            return MetricReading<MemoryUsage>.Unavailable("vm_stat did not report free pages.");
        }

        var inactive = ReadPages(vmStat, "Pages inactive") ?? 0;
        var speculative = ReadPages(vmStat, "Pages speculative") ?? 0;
        var availableBytes = (free.Value + inactive + speculative) * pageSize;
        var usedBytes = Math.Clamp(totalBytes - availableBytes, 0, totalBytes);
        return MetricReading<MemoryUsage>.Available(new MemoryUsage(usedBytes, totalBytes));
    }

    private static Dictionary<string, long> ReadMeminfo(string meminfo)
    {
        var values = new Dictionary<string, long>(StringComparer.Ordinal);
        var lines = meminfo.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var match = MeminfoLine().Match(lines[i].Trim());
            if (match.Success && long.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var kibibytes))
            {
                values[match.Groups[1].Value] = kibibytes;
            }
        }

        return values;
    }

    private static long? FindAvailable(Dictionary<string, long> values)
    {
        if (values.TryGetValue("MemAvailable", out var available))
        {
            return available;
        }

        if (!values.TryGetValue("MemFree", out var free))
        {
            return null;
        }

        values.TryGetValue("Buffers", out var buffers);
        values.TryGetValue("Cached", out var cached);
        return free + buffers + cached;
    }

    private static long? ReadPages(string vmStat, string label)
    {
        var pattern = $@"^{Regex.Escape(label)}:\s+(\d+)\.?\s*$";
        var match = Regex.Match(vmStat, pattern, RegexOptions.Multiline | RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return null;
        }

        return long.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    [GeneratedRegex(@"^(\w+(?:\(\w+\))?):\s+(\d+)(?:\s*kB)?$", RegexOptions.CultureInvariant)]
    private static partial Regex MeminfoLine();

    [GeneratedRegex(@"page size of (\d+) bytes", RegexOptions.CultureInvariant)]
    private static partial Regex PageSize();
}
