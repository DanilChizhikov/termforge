using DTech.Core.Monitoring;

namespace DTech.Entrypoint.Monitoring;

public sealed class ServerMetricsResponse
{
    public required Guid ConnectionId { get; init; }

    public required string State { get; init; }

    public required DateTime CollectedAt { get; init; }

    public string? Message { get; init; }

    public long? LatencyMilliseconds { get; init; }

    public required MetricResponse<double> UptimeSeconds { get; init; }

    public required MetricResponse<double> CpuPercent { get; init; }

    public required MetricResponse<MemoryUsage> Memory { get; init; }

    public required MetricResponse<DiskUsage> Disk { get; init; }

    public required MetricResponse<LoadAverage> LoadAverage { get; init; }

    public required MetricResponse<int> CpuCount { get; init; }

    public static ServerMetricsResponse From(ServerMetrics metrics)
    {
        var readings = metrics.Readings;
        return new ServerMetricsResponse
        {
            ConnectionId = metrics.ConnectionId,
            State = metrics.State.ToString(),
            CollectedAt = metrics.CollectedAt,
            Message = metrics.Message,
            LatencyMilliseconds = metrics.LatencyMilliseconds,
            UptimeSeconds = MetricResponse<double>.FromUptime(readings.Uptime),
            CpuPercent = MetricResponse<double>.From(readings.CpuPercent),
            Memory = MetricResponse<MemoryUsage>.From(readings.Memory),
            Disk = MetricResponse<DiskUsage>.From(readings.Disk),
            LoadAverage = MetricResponse<LoadAverage>.From(readings.LoadAverage),
            CpuCount = MetricResponse<int>.From(readings.CpuCount),
        };
    }
}
