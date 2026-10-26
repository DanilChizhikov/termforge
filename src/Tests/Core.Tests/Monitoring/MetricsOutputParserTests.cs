using DTech.Core.Monitoring;
using DTech.Core.Monitoring.Parsing;
using DTech.Core.Tests.Monitoring.Support;
using FluentAssertions;

namespace DTech.Core.Tests.Monitoring;

[TestClass]
public sealed class MetricsOutputParserTests
{
    private const long KibiByte = 1024;

    [TestMethod]
    public void Parse_GlibcLinux_ReadsAllMetrics()
    {
        var readings = MetricsOutputParser.Parse(RecordedMetricOutputs.GlibcLinux);

        readings.CpuPercent.Value.Should().Be(48.5);
        readings.Uptime.Value.Should().Be(TimeSpan.FromSeconds(1048576));
        readings.Memory.Value.Should().Be(new MemoryUsage((4046428 - 2046428) * KibiByte, 4046428 * KibiByte));
        readings.Disk.Value.Should().Be(new DiskUsage(25836480 * KibiByte, 82836480 * KibiByte, "/"));
    }

    [TestMethod]
    public void Parse_BusyBoxLinux_FallsBackWhenMemAvailableIsMissing()
    {
        var readings = MetricsOutputParser.Parse(RecordedMetricOutputs.BusyBoxLinuxWithoutMemAvailable);

        readings.Memory.Value.Should().Be(new MemoryUsage(500000 * KibiByte, 1000000 * KibiByte));
        readings.CpuPercent.Value.Should().Be(30.0);
        readings.Uptime.Value.Should().Be(TimeSpan.FromSeconds(3600));
        readings.Disk.Value!.Value.MountPoint.Should().Be("/");
        readings.Disk.Value!.Value.FreeBytes.Should().Be(49000000 * KibiByte);
    }

    [TestMethod]
    public void Parse_MacOs_ReadsMetricsFromSysctlTopAndVmStat()
    {
        var readings = MetricsOutputParser.Parse(RecordedMetricOutputs.MacOs);

        readings.CpuPercent.Value.Should().Be(15.8);
        readings.Uptime.Value.Should().Be(TimeSpan.FromSeconds(90000));
        readings.Memory.Value.Should().Be(new MemoryUsage(17179869184 - 310000L * 16384, 17179869184));
        readings.Disk.Value!.Value.FreeBytes.Should().Be(500000000L * KibiByte);
    }

    [TestMethod]
    public void Parse_FreeBsd_ReadsUptimeAndDiskAndExplainsMissingCpuAndMemory()
    {
        var readings = MetricsOutputParser.Parse(RecordedMetricOutputs.FreeBsd);

        readings.Uptime.Value.Should().Be(TimeSpan.FromHours(1));
        readings.Disk.IsAvailable.Should().BeTrue();
        readings.CpuPercent.Reason.Should().Contain("FreeBSD");
        readings.Memory.Reason.Should().Contain("FreeBSD");
    }

    [TestMethod]
    public void Parse_LinuxWithoutTools_ReportsReasonForEveryMetric()
    {
        var readings = MetricsOutputParser.Parse(RecordedMetricOutputs.LinuxWithoutTools);

        AssertUnavailable(readings.CpuPercent.IsAvailable, readings.CpuPercent.Value is null, readings.CpuPercent.Reason);
        AssertUnavailable(readings.Uptime.IsAvailable, readings.Uptime.Value is null, readings.Uptime.Reason);
        AssertUnavailable(readings.Memory.IsAvailable, readings.Memory.Value is null, readings.Memory.Reason);
        AssertUnavailable(readings.Disk.IsAvailable, readings.Disk.Value is null, readings.Disk.Reason);
    }

    [TestMethod]
    public void Parse_Garbage_NeverProducesValues()
    {
        var readings = MetricsOutputParser.Parse(RecordedMetricOutputs.Garbage);

        readings.CpuPercent.IsAvailable.Should().BeFalse();
        readings.Uptime.IsAvailable.Should().BeFalse();
        readings.Memory.IsAvailable.Should().BeFalse();
        readings.Disk.IsAvailable.Should().BeFalse();
        readings.Uptime.Reason.Should().Contain("operating system");
    }

    [TestMethod]
    public void Parse_EmptyOutput_NeverProducesValues()
    {
        var readings = MetricsOutputParser.Parse(string.Empty);

        readings.CpuPercent.Reason.Should().NotBeNullOrWhiteSpace();
        readings.Disk.Reason.Should().NotBeNullOrWhiteSpace();
    }

    [TestMethod]
    public void CpuProcStat_WhenCountersDoNotAdvance_IsUnavailable()
    {
        const string sample = "cpu  100 0 50 800 0 0 0 0";

        var reading = CpuUsageParser.ParseProcStat(sample, sample);

        reading.IsAvailable.Should().BeFalse();
        reading.Reason.Should().Contain("did not advance");
    }

    [TestMethod]
    public void CpuProcStat_WithCommaDecimalTop_ParsesIdle()
    {
        var reading = CpuUsageParser.ParseTop("CPU usage: 1,5% user, 3,5% sys, 95,0% idle");

        reading.Value.Should().Be(5.0);
    }

    [TestMethod]
    public void MemoryMeminfo_WhenUsedWouldBeNegative_ClampsToZero()
    {
        const string meminfo = "MemTotal: 1000 kB\nMemAvailable: 5000 kB";

        var reading = MemoryUsageParser.ParseMeminfo(meminfo);

        reading.Value!.Value.UsedBytes.Should().Be(0);
    }

    [TestMethod]
    public void DiskDf_WithMountPointContainingSpaces_KeepsFullPath()
    {
        const string df = "Filesystem 1024-blocks Used Available Capacity Mounted on\n/dev/disk2 1000 400 600 40% /Volumes/My Disk";

        var reading = DiskUsageParser.ParseDf(df);

        reading.Value!.Value.MountPoint.Should().Be("/Volumes/My Disk");
    }

    [TestMethod]
    public void UptimeBootTime_WhenBootIsInTheFuture_IsUnavailable()
    {
        var reading = UptimeParser.ParseBootTime("{ sec = 2000, usec = 0 }", "1000");

        reading.IsAvailable.Should().BeFalse();
    }

    private static void AssertUnavailable(bool isAvailable, bool hasNoValue, string? reason)
    {
        isAvailable.Should().BeFalse();
        hasNoValue.Should().BeTrue();
        reason.Should().NotBeNullOrWhiteSpace();
    }
}
