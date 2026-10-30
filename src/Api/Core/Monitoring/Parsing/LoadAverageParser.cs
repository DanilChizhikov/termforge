using System.Globalization;

namespace DTech.Core.Monitoring.Parsing;

public static class LoadAverageParser
{
    private const int RequiredValueCount = 3;
    private const string UnreadableReason = "The load average could not be read or has an unexpected format.";

    public static MetricReading<LoadAverage> Parse(string output)
    {
        var fields = output.Split([' ', '\t', '\n', '{', '}'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (fields.Length < RequiredValueCount)
        {
            return MetricReading<LoadAverage>.Unavailable(UnreadableReason);
        }

        var values = new double[RequiredValueCount];
        for (var i = 0; i < RequiredValueCount; i++)
        {
            var normalized = fields[i].Replace(',', '.');
            if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || value < 0)
            {
                return MetricReading<LoadAverage>.Unavailable(UnreadableReason);
            }

            values[i] = value;
        }

        var loadAverage = new LoadAverage(values[0], values[1], values[2]);
        return MetricReading<LoadAverage>.Available(loadAverage);
    }
}
