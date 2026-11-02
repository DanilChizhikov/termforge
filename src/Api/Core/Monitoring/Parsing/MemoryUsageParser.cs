using System.Globalization;
using System.Text.RegularExpressions;

namespace DTech.Core.Monitoring.Parsing;

public static partial class MemoryUsageParser
{
    private const long KibiByte = 1024;

    public static MetricReading<MemoryUsage> ParseMeminfo(string meminfo)
    {
        var values = ReadMeminfo(meminfo);
        if (values is null || !values.TryGetValue("MemTotal", out var total) || total <= 0 || total > long.MaxValue / KibiByte)
        {
            return MetricReading<MemoryUsage>.Unavailable("/proc/meminfo did not report a valid MemTotal within the supported range.");
        }

        var available = FindAvailable(values);
        if (available is null || available > long.MaxValue / KibiByte)
        {
            return MetricReading<MemoryUsage>.Unavailable("/proc/meminfo did not report valid available memory within the supported range.");
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
        if (!pageSizeMatch.Success || !long.TryParse(pageSizeMatch.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var pageSize) || pageSize <= 0)
        {
            return MetricReading<MemoryUsage>.Unavailable("vm_stat did not report a page size.");
        }

        if (!TryReadPages(vmStat, "Pages free", out var free) || free is null ||
            !TryReadPages(vmStat, "Pages inactive", out var inactive) ||
            !TryReadPages(vmStat, "Pages speculative", out var speculative))
        {
            return MetricReading<MemoryUsage>.Unavailable("vm_stat did not report free pages.");
        }

        if (!TrySum(free.Value, inactive ?? 0, out var availablePages) ||
            !TrySum(availablePages, speculative ?? 0, out availablePages) || availablePages > long.MaxValue / pageSize)
        {
            return MetricReading<MemoryUsage>.Unavailable("vm_stat reported memory values outside the supported range.");
        }

        var availableBytes = availablePages * pageSize;
        var usedBytes = Math.Clamp(totalBytes - availableBytes, 0, totalBytes);
        return MetricReading<MemoryUsage>.Available(new MemoryUsage(usedBytes, totalBytes));
    }

    private static Dictionary<string, long>? ReadMeminfo(string meminfo)
    {
        var values = new Dictionary<string, long>(StringComparer.Ordinal);
        var lines = meminfo.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            var separator = line.IndexOf(':');
            if (separator < 0)
            {
                continue;
            }

            var name = line[..separator];
            if (name is not ("MemTotal" or "MemAvailable" or "MemFree" or "Buffers" or "Cached"))
            {
                continue;
            }

            var match = MeminfoLine().Match(line);
            if (!match.Success || !long.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var kibibytes))
            {
                return null;
            }

            values[name] = kibibytes;
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
        return TrySum(free, buffers, out var availableBytes) && TrySum(availableBytes, cached, out availableBytes)
            ? availableBytes
            : null;
    }

    private static bool TryReadPages(string vmStat, string label, out long? pages)
    {
        pages = null;
        var escapedLabel = Regex.Escape(label);
        var pattern = $@"^{escapedLabel}:\s+(\d+)\.?\s*$";
        var match = Regex.Match(vmStat, pattern, RegexOptions.Multiline | RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            var prefix = $@"^{escapedLabel}:";
            return !Regex.IsMatch(vmStat, prefix, RegexOptions.Multiline | RegexOptions.CultureInvariant);
        }

        if (!long.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        pages = value;
        return true;
    }

    private static bool TrySum(long first, long second, out long total)
    {
        total = 0;
        if (second > long.MaxValue - first)
        {
            return false;
        }

        total = first + second;
        return true;
    }

    [GeneratedRegex(@"^(\w+(?:\(\w+\))?):\s+(\d+)(?:\s*kB)?$", RegexOptions.CultureInvariant)]
    private static partial Regex MeminfoLine();

    [GeneratedRegex(@"page size of (\d+) bytes", RegexOptions.CultureInvariant)]
    private static partial Regex PageSize();
}
