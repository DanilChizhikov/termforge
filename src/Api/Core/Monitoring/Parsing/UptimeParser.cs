using System.Globalization;
using System.Text.RegularExpressions;

namespace DTech.Core.Monitoring.Parsing;

public static partial class UptimeParser
{
    public static MetricReading<TimeSpan> ParseProcUptime(string procUptime)
    {
        var fields = procUptime.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (fields.Length == 0 || !double.TryParse(fields[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) || seconds < 0)
        {
            return MetricReading<TimeSpan>.Unavailable("/proc/uptime could not be read or has an unexpected format.");
        }

        return MetricReading<TimeSpan>.Available(TimeSpan.FromSeconds(Math.Floor(seconds)));
    }

    public static MetricReading<TimeSpan> ParseBootTime(string bootTime, string now)
    {
        var bootSeconds = ReadBootSeconds(bootTime);
        if (bootSeconds is null)
        {
            return MetricReading<TimeSpan>.Unavailable("sysctl kern.boottime did not report the boot time.");
        }

        if (!long.TryParse(now.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var nowSeconds))
        {
            return MetricReading<TimeSpan>.Unavailable("date did not report the current time.");
        }

        var uptimeSeconds = nowSeconds - bootSeconds.Value;
        if (uptimeSeconds < 0)
        {
            return MetricReading<TimeSpan>.Unavailable("The boot time is later than the current time on the server.");
        }

        return MetricReading<TimeSpan>.Available(TimeSpan.FromSeconds(uptimeSeconds));
    }

    private static long? ReadBootSeconds(string bootTime)
    {
        var match = BootSeconds().Match(bootTime);
        if (match.Success)
        {
            return long.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        var plain = bootTime.Trim();
        return long.TryParse(plain, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) ? seconds : null;
    }

    [GeneratedRegex(@"sec\s*=\s*(\d+)", RegexOptions.CultureInvariant)]
    private static partial Regex BootSeconds();
}
