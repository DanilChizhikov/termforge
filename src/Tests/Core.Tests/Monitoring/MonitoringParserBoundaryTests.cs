using DTech.Core.Monitoring.Parsing;
using DTech.Core.Tests.Monitoring.Support;
using FluentAssertions;

namespace DTech.Core.Tests.Monitoring;

[TestClass]
public sealed class MonitoringParserBoundaryTests
{
    [TestMethod]
    [DataRow("NaN")]
    [DataRow("Infinity")]
    [DataRow("-Infinity")]
    [DataRow("1e309")]
    [DataRow("922337203686")]
    [DataRow("-1")]
    public void ProcUptime_InvalidOrOutOfRange_IsUnavailable(string seconds)
    {
        var result = UptimeParser.ParseProcUptime(seconds);

        result.IsAvailable.Should().BeFalse();
        result.Reason.Should().NotBeNullOrWhiteSpace();
    }

    [TestMethod]
    [DataRow("{ sec = 9223372036854775808, usec = 0 }", "1000")]
    [DataRow("{ usec = 100 }", "1000")]
    [DataRow("0", "9223372036854775807")]
    [DataRow("-1", "1000")]
    public void BootTime_InvalidOrOverflowing_IsUnavailable(string boot, string now)
    {
        var result = UptimeParser.ParseBootTime(boot, now);

        result.IsAvailable.Should().BeFalse();
    }

    [TestMethod]
    public void Uptime_LargestWholeSecondValue_IsSupportedWithoutRoundingOverflow()
    {
        const long maximum = long.MaxValue / TimeSpan.TicksPerSecond;
        var seconds = maximum.ToString(System.Globalization.CultureInfo.InvariantCulture);

        var proc = UptimeParser.ParseProcUptime(seconds);
        var boot = UptimeParser.ParseBootTime("0", seconds);

        proc.Value!.Value.Ticks.Should().Be(maximum * TimeSpan.TicksPerSecond);
        boot.Value.Should().Be(proc.Value);
    }

    [TestMethod]
    [DataRow("9223372036854775807 0 0")]
    [DataRow("1000 9223372036854775808 0")]
    [DataRow("1000 0 9223372036854775808")]
    [DataRow("1000 0 1001")]
    [DataRow("1000 -1 10")]
    public void Disk_InvalidCountsOrByteConversion_IsUnavailable(string counts)
    {
        var output = $"/dev/test {counts} 0% /";

        var result = DiskUsageParser.ParseDf(output);

        result.IsAvailable.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("MemTotal: 9223372036854775807 kB\nMemAvailable: 0 kB")]
    [DataRow("MemTotal: 1000 kB\nMemAvailable: 9223372036854775807 kB")]
    [DataRow("MemTotal: 1000 kB\nMemAvailable: 9223372036854775808 kB\nMemFree: 10 kB")]
    [DataRow("MemTotal: 1000 kB\nMemFree: 9223372036854775807 kB\nBuffers: 1 kB")]
    [DataRow("MemTotal: 1000 kB\nMemFree: -1 kB")]
    public void Meminfo_InvalidCountsOrArithmetic_IsUnavailable(string meminfo)
    {
        var result = MemoryUsageParser.ParseMeminfo(meminfo);

        result.IsAvailable.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("0", "1", "0")]
    [DataRow("9223372036854775808", "1", "0")]
    [DataRow("2", "9223372036854775807", "0")]
    [DataRow("1", "9223372036854775807", "1")]
    [DataRow("4096", "1", "9223372036854775808")]
    [DataRow("4096", "-1", "0")]
    public void VmStat_InvalidPageCountsOrArithmetic_IsUnavailable(string pageSize, string free, string inactive)
    {
        var vmStat = $"Mach Virtual Memory Statistics: (page size of {pageSize} bytes)\nPages free: {free}.\nPages inactive: {inactive}.";

        var result = MemoryUsageParser.ParseVmStat("9223372036854775807", vmStat);

        result.IsAvailable.Should().BeFalse();
    }

    [TestMethod]
    public void CpuCounterSumOverflow_IsUnavailable()
    {
        const string before = "cpu 9223372036854775807 1 0 0";
        const string after = "cpu 9223372036854775807 2 0 0";

        var result = CpuUsageParser.ParseProcStat(before, after);

        result.IsAvailable.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("NaN 0 0")]
    [DataRow("Infinity 0 0")]
    [DataRow("1e309 0 0")]
    public void NonFiniteLoadAverage_IsUnavailable(string output)
    {
        var result = LoadAverageParser.Parse(output);

        result.IsAvailable.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("CPU usage: 101% idle")]
    [DataRow("CPU usage: -1% idle")]
    [DataRow("CPU usage: 99999999999999999999999999999999999999999999999999999% idle")]
    public void InvalidCpuIdlePercentage_IsUnavailable(string output)
    {
        var result = CpuUsageParser.ParseTop(output);

        result.IsAvailable.Should().BeFalse();
    }

    [TestMethod]
    public void MalformedMemory_DoesNotDiscardTheOtherMetrics()
    {
        var output = RecordedMetricOutputs.GlibcLinux.Replace("4046428 kB", "9223372036854775807 kB", StringComparison.Ordinal);

        var readings = MetricsOutputParser.Parse(output);

        readings.Memory.IsAvailable.Should().BeFalse();
        readings.CpuPercent.IsAvailable.Should().BeTrue();
        readings.Uptime.IsAvailable.Should().BeTrue();
        readings.Disk.IsAvailable.Should().BeTrue();
        readings.LoadAverage.IsAvailable.Should().BeTrue();
    }
}
