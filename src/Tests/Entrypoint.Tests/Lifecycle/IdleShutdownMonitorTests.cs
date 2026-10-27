using DTech.Entrypoint.Lifecycle;
using FluentAssertions;

namespace DTech.Entrypoint.Tests.Lifecycle;

[TestClass]
public sealed class IdleShutdownMonitorTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(30);

    [TestMethod]
    public void ShouldShutdown_IdleShorterThanTimeout_IsFalse()
    {
        var monitor = new IdleShutdownMonitor(IdleTimeout);
        var almostTimedOut = Start + IdleTimeout - TimeSpan.FromSeconds(1);

        monitor.ShouldShutdown(true, Start).Should().BeFalse();
        monitor.ShouldShutdown(true, almostTimedOut).Should().BeFalse();
    }

    [TestMethod]
    public void ShouldShutdown_IdleForTimeout_IsTrue()
    {
        var monitor = new IdleShutdownMonitor(IdleTimeout);
        var timedOut = Start + IdleTimeout;

        monitor.ShouldShutdown(true, Start).Should().BeFalse();
        monitor.ShouldShutdown(true, timedOut).Should().BeTrue();
    }

    [TestMethod]
    public void ShouldShutdown_ActivityInBetween_RestartsTheIdlePeriod()
    {
        var monitor = new IdleShutdownMonitor(IdleTimeout);
        var activeAt = Start + TimeSpan.FromMinutes(20);
        var idleAgainAt = Start + TimeSpan.FromMinutes(21);
        var originalDeadline = Start + IdleTimeout;
        var restartedDeadline = idleAgainAt + IdleTimeout;

        monitor.ShouldShutdown(true, Start);
        monitor.ShouldShutdown(false, activeAt).Should().BeFalse();
        monitor.ShouldShutdown(true, idleAgainAt);

        monitor.ShouldShutdown(true, originalDeadline).Should().BeFalse();
        monitor.ShouldShutdown(true, restartedDeadline).Should().BeTrue();
    }
}
