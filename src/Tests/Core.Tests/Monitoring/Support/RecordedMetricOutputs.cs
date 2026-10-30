namespace DTech.Core.Tests.Monitoring.Support;

internal static class RecordedMetricOutputs
{
    public const string GlibcLinux = """
        @@os
        Linux
        @@cpu1
        cpu  10000 200 3000 80000 500 0 100 0 0 0
        @@cpu2
        cpu  10100 200 3050 80150 520 0 110 0 0 0
        @@uptime
        1048576.55 2000000.10
        @@mem
        MemTotal:        4046428 kB
        MemFree:          501000 kB
        MemAvailable:    2046428 kB
        Buffers:          100000 kB
        Cached:          1200000 kB
        SwapTotal:       2097148 kB
        HugePages_Total:       0
        @@disk
        Filesystem     1024-blocks      Used Available Capacity Mounted on
        /dev/sda1         82836480  57000000  25836480      69% /
        @@loadavg
        0.42 0.38 0.35 1/234 5678
        @@cpucount
        2
        """;

    public const string BusyBoxLinuxWithoutMemAvailable = """
        @@os
        Linux
        @@cpu1
        cpu  100 0 50 800 0 0 0 0
        @@cpu2
        cpu  110 0 55 835 0 0 0 0
        @@uptime
        3600.25 7000.00
        @@mem
        MemTotal:        1000000 kB
        MemFree:          200000 kB
        Buffers:           50000 kB
        Cached:           250000 kB
        @@disk
        Filesystem           1024-blocks    Used Available Capacity Mounted on
        overlay                 61255492 9000000  49000000      16% /
        """;

    public const string MacOs = """
        @@os
        Darwin
        @@boottime
        { sec = 1790000000, usec = 123456 } Mon Oct 12 10:00:00 2026
        @@now
        1790090000
        @@cputop
        CPU usage: 5.26% user, 10.52% sys, 84.21% idle
        @@memsize
        17179869184
        @@vmstat
        Mach Virtual Memory Statistics: (page size of 16384 bytes)
        Pages free:                               20000.
        Pages active:                            300000.
        Pages inactive:                          280000.
        Pages speculative:                        10000.
        Pages throttled:                              0.
        @@disk
        Filesystem       1024-blocks      Used Available Capacity  Mounted on
        /dev/disk3s1s1     971350180  15000000 500000000     3%    /
        @@loadavg
        { 1,85 2,10 2,25 }
        @@cpucount
        10
        """;

    public const string FreeBsd = """
        @@os
        FreeBSD
        @@boottime
        { sec = 1790000000, usec = 0 } Mon Oct 12 10:00:00 2026
        @@now
        1790003600
        @@disk
        Filesystem 1024-blocks    Used   Avail Capacity  Mounted on
        /dev/ada0p2   20000000 5000000 13400000      27%    /
        @@loadavg
        { 0.10 0.20 0.30 }
        @@cpucount
        4
        """;

    public const string LinuxWithoutTools = """
        @@os
        Linux
        @@cpu1
        @@cpu2
        @@uptime
        @@mem
        @@disk
        @@loadavg
        @@cpucount
        """;

    public const string Garbage = "total nonsense\n\u0000\u0001 @@ @@@\nstill nothing useful";
}
