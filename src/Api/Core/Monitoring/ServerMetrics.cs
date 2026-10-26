namespace DTech.Core.Monitoring;

public sealed record ServerMetrics(Guid ConnectionId, DateTime CollectedAt, MonitoringState State, MetricReadings Readings)
{
    public string? Message { get; init; }
}
