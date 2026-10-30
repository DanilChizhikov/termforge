namespace DTech.Core.Monitoring.Parsing;

public readonly record struct SystemReadings(MetricReading<TimeSpan> Uptime, MetricReading<double> CpuPercent, MetricReading<MemoryUsage> Memory);
