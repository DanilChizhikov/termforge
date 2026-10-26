namespace DTech.Core.Monitoring;

public readonly record struct DiskUsage(long FreeBytes, long TotalBytes, string MountPoint);
