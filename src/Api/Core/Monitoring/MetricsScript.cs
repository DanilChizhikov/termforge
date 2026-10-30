namespace DTech.Core.Monitoring;

public static class MetricsScript
{
    public const string OsSection = "os";
    public const string CpuFirstSection = "cpu1";
    public const string CpuSecondSection = "cpu2";
    public const string CpuTopSection = "cputop";
    public const string UptimeSection = "uptime";
    public const string MemorySection = "mem";
    public const string MemorySizeSection = "memsize";
    public const string VmStatSection = "vmstat";
    public const string BootTimeSection = "boottime";
    public const string NowSection = "now";
    public const string DiskSection = "disk";
    public const string LoadAverageSection = "loadavg";
    public const string CpuCountSection = "cpucount";
    public const string SectionPrefix = "@@";

    public const string Text = """
        os=$(uname -s 2>/dev/null)
        echo "@@os"
        echo "$os"
        case "$os" in
        Linux)
          echo "@@cpu1"
          head -n 1 /proc/stat 2>/dev/null
          sleep 1
          echo "@@cpu2"
          head -n 1 /proc/stat 2>/dev/null
          echo "@@uptime"
          cat /proc/uptime 2>/dev/null
          echo "@@mem"
          cat /proc/meminfo 2>/dev/null
          ;;
        Darwin|FreeBSD|OpenBSD|NetBSD|DragonFly)
          echo "@@boottime"
          sysctl -n kern.boottime 2>/dev/null
          echo "@@now"
          date +%s
          ;;
        esac
        if [ "$os" = "Darwin" ]; then
          echo "@@cputop"
          top -l 1 -n 0 2>/dev/null | grep 'CPU usage'
          echo "@@memsize"
          sysctl -n hw.memsize 2>/dev/null
          echo "@@vmstat"
          vm_stat 2>/dev/null
        fi
        echo "@@disk"
        df -Pk / 2>/dev/null
        echo "@@loadavg"
        if [ -r /proc/loadavg ]; then
          cat /proc/loadavg 2>/dev/null
        else
          sysctl -n vm.loadavg 2>/dev/null
        fi
        echo "@@cpucount"
        getconf _NPROCESSORS_ONLN 2>/dev/null || nproc 2>/dev/null || sysctl -n hw.ncpu 2>/dev/null
        """;
}
