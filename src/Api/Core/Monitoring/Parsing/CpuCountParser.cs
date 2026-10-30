using System.Globalization;

namespace DTech.Core.Monitoring.Parsing;

public static class CpuCountParser
{
    public static MetricReading<int> Parse(string output)
    {
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length == 0 || !int.TryParse(lines[0], NumberStyles.None, CultureInfo.InvariantCulture, out var count) || count <= 0)
        {
            return MetricReading<int>.Unavailable("The number of CPUs could not be read.");
        }

        return MetricReading<int>.Available(count);
    }
}
