namespace DTech.Core.Monitoring;

public sealed record MetricReading<TValue> where TValue : struct
{
    private MetricReading(TValue? value, string? reason)
    {
        Value = value;
        Reason = reason;
    }

    public TValue? Value { get; }

    public string? Reason { get; }

    public bool IsAvailable => Value.HasValue;

    public static MetricReading<TValue> Available(TValue value)
    {
        return new MetricReading<TValue>(value, null);
    }

    public static MetricReading<TValue> Unavailable(string reason)
    {
        return new MetricReading<TValue>(null, reason);
    }
}
