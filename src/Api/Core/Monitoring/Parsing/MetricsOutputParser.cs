namespace DTech.Core.Monitoring.Parsing;

public static class MetricsOutputParser
{
    public static MetricReadings Parse(string output)
    {
        var sections = MetricSections.Parse(output);
        var os = sections.Get(MetricsScript.OsSection);
        var disk = ReadDisk(sections);
        if (os == "Linux")
        {
            return ParseLinux(sections, disk);
        }

        if (os == "Darwin")
        {
            return ParseDarwin(sections, disk);
        }

        return ParseOther(os, sections, disk);
    }

    private static MetricReadings ParseLinux(MetricSections sections, MetricReading<DiskUsage> disk)
    {
        var first = sections.Get(MetricsScript.CpuFirstSection);
        var second = sections.Get(MetricsScript.CpuSecondSection);
        var cpu = CpuUsageParser.ParseProcStat(first, second);
        var procUptime = sections.Get(MetricsScript.UptimeSection);
        var uptime = UptimeParser.ParseProcUptime(procUptime);
        var meminfo = sections.Get(MetricsScript.MemorySection);
        var memory = MemoryUsageParser.ParseMeminfo(meminfo);
        return new MetricReadings(uptime, cpu, memory, disk);
    }

    private static MetricReadings ParseDarwin(MetricSections sections, MetricReading<DiskUsage> disk)
    {
        var topLine = sections.Get(MetricsScript.CpuTopSection);
        var cpu = CpuUsageParser.ParseTop(topLine);
        var uptime = ReadBootTimeUptime(sections);
        var memorySize = sections.Get(MetricsScript.MemorySizeSection);
        var vmStat = sections.Get(MetricsScript.VmStatSection);
        var memory = MemoryUsageParser.ParseVmStat(memorySize, vmStat);
        return new MetricReadings(uptime, cpu, memory, disk);
    }

    private static MetricReadings ParseOther(string os, MetricSections sections, MetricReading<DiskUsage> disk)
    {
        var hasOs = !string.IsNullOrWhiteSpace(os);
        var name = hasOs ? os : "this operating system";
        var reason = $"CPU and memory collection is not supported on {name}.";
        var cpu = MetricReading<double>.Unavailable(reason);
        var memory = MetricReading<MemoryUsage>.Unavailable(reason);
        var uptime = hasOs
            ? ReadBootTimeUptime(sections)
            : MetricReading<TimeSpan>.Unavailable("The server did not report its operating system.");
        return new MetricReadings(uptime, cpu, memory, disk);
    }

    private static MetricReading<TimeSpan> ReadBootTimeUptime(MetricSections sections)
    {
        var bootTime = sections.Get(MetricsScript.BootTimeSection);
        var now = sections.Get(MetricsScript.NowSection);
        return UptimeParser.ParseBootTime(bootTime, now);
    }

    private static MetricReading<DiskUsage> ReadDisk(MetricSections sections)
    {
        var df = sections.Get(MetricsScript.DiskSection);
        return DiskUsageParser.ParseDf(df);
    }
}
