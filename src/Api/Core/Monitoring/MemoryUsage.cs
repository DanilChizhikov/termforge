namespace DTech.Core.Monitoring;

public readonly record struct MemoryUsage(long UsedBytes, long TotalBytes);
