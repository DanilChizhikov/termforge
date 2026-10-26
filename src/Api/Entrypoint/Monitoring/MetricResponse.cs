using DTech.Core.Monitoring;

namespace DTech.Entrypoint.Monitoring;

public sealed class MetricResponse<TValue> where TValue : struct
{
    public required bool Available { get; init; }

    public TValue? Value { get; init; }

    public string? Reason { get; init; }

    public static MetricResponse<TValue> From(MetricReading<TValue> reading)
    {
        return new MetricResponse<TValue>
        {
            Available = reading.IsAvailable,
            Value = reading.Value,
            Reason = reading.Reason,
        };
    }

    public static MetricResponse<double> FromUptime(MetricReading<TimeSpan> reading)
    {
        return new MetricResponse<double>
        {
            Available = reading.IsAvailable,
            Value = reading.Value?.TotalSeconds,
            Reason = reading.Reason,
        };
    }
}
