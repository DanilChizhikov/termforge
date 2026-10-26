namespace DTech.Core.Monitoring;

public sealed record MetricReadings(
    MetricReading<TimeSpan> Uptime,
    MetricReading<double> CpuPercent,
    MetricReading<MemoryUsage> Memory,
    MetricReading<DiskUsage> Disk)
{
    public static MetricReadings AllUnavailable(string reason)
    {
        return new MetricReadings(
            MetricReading<TimeSpan>.Unavailable(reason),
            MetricReading<double>.Unavailable(reason),
            MetricReading<MemoryUsage>.Unavailable(reason),
            MetricReading<DiskUsage>.Unavailable(reason));
    }
}
