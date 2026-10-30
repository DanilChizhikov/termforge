namespace DTech.Core.Monitoring.Parsing;

public static class MetricsOutputParser
{
    public static MetricReadings Parse(string output)
    {
        var sections = MetricSections.Parse(output);
        var os = sections.Get(MetricsScript.OsSection);
        var system = ParseSystem(os, sections);
        var df = sections.Get(MetricsScript.DiskSection);
        var disk = DiskUsageParser.ParseDf(df);
        var loadAverageOutput = sections.Get(MetricsScript.LoadAverageSection);
        var loadAverage = LoadAverageParser.Parse(loadAverageOutput);
        var cpuCountOutput = sections.Get(MetricsScript.CpuCountSection);
        var cpuCount = CpuCountParser.Parse(cpuCountOutput);
        return new MetricReadings(system.Uptime, system.CpuPercent, system.Memory, disk, loadAverage, cpuCount);
    }

    private static SystemReadings ParseSystem(string os, MetricSections sections)
    {
        if (os == "Linux")
        {
            return ParseLinux(sections);
        }

        if (os == "Darwin")
        {
            return ParseDarwin(sections);
        }

        return ParseOther(os, sections);
    }

    private static SystemReadings ParseLinux(MetricSections sections)
    {
        var first = sections.Get(MetricsScript.CpuFirstSection);
        var second = sections.Get(MetricsScript.CpuSecondSection);
        var cpu = CpuUsageParser.ParseProcStat(first, second);
        var procUptime = sections.Get(MetricsScript.UptimeSection);
        var uptime = UptimeParser.ParseProcUptime(procUptime);
        var meminfo = sections.Get(MetricsScript.MemorySection);
        var memory = MemoryUsageParser.ParseMeminfo(meminfo);
        return new SystemReadings(uptime, cpu, memory);
    }

    private static SystemReadings ParseDarwin(MetricSections sections)
    {
        var topLine = sections.Get(MetricsScript.CpuTopSection);
        var cpu = CpuUsageParser.ParseTop(topLine);
        var uptime = ReadBootTimeUptime(sections);
        var memorySize = sections.Get(MetricsScript.MemorySizeSection);
        var vmStat = sections.Get(MetricsScript.VmStatSection);
        var memory = MemoryUsageParser.ParseVmStat(memorySize, vmStat);
        return new SystemReadings(uptime, cpu, memory);
    }

    private static SystemReadings ParseOther(string os, MetricSections sections)
    {
        var hasOs = !string.IsNullOrWhiteSpace(os);
        var name = hasOs ? os : "this operating system";
        var reason = $"CPU and memory collection is not supported on {name}.";
        var cpu = MetricReading<double>.Unavailable(reason);
        var memory = MetricReading<MemoryUsage>.Unavailable(reason);
        var uptime = hasOs
            ? ReadBootTimeUptime(sections)
            : MetricReading<TimeSpan>.Unavailable("The server did not report its operating system.");
        return new SystemReadings(uptime, cpu, memory);
    }

    private static MetricReading<TimeSpan> ReadBootTimeUptime(MetricSections sections)
    {
        var bootTime = sections.Get(MetricsScript.BootTimeSection);
        var now = sections.Get(MetricsScript.NowSection);
        return UptimeParser.ParseBootTime(bootTime, now);
    }
}
