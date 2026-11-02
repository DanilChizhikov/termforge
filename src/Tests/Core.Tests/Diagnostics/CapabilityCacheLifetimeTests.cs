using DTech.Core.Diagnostics;
using DTech.Core.SavedConnections;
using FluentAssertions;

namespace DTech.Core.Tests.Diagnostics;

[TestClass]
public sealed class CapabilityCacheLifetimeTests
{
    [TestMethod]
    public void Cache_ExpiresAtFiveMinutesUsingTheProvidedClock()
    {
        var clock = new ManualClock();
        var cache = new CapabilityReportCache(clock);
        var id = Guid.NewGuid();
        var report = Report(id);
        cache.Store(report);

        var tick = TimeSpan.FromTicks(1);
        var beforeExpiry = TimeSpan.FromMinutes(5) - tick;
        clock.Advance(beforeExpiry);
        cache.Find(report.ConnectionId).Should().BeSameAs(report);
        clock.Advance(tick);
        cache.Find(report.ConnectionId).Should().BeNull();
    }

    [TestMethod]
    public void Invalidation_PreventsAnEarlierRefreshFromRepopulatingTheCache()
    {
        var cache = new CapabilityReportCache();
        var id = Guid.NewGuid();
        var report = Report(id);
        var generation = cache.Generation;

        cache.Invalidate(report.ConnectionId, []);
        var stored = cache.Store(report, generation);

        stored.Should().BeFalse();
        cache.Find(report.ConnectionId).Should().BeNull();
        cache.Store(report, cache.Generation).Should().BeTrue();
    }

    [TestMethod]
    public void InvalidateJump_RemovesAllDescendantsAndKeepsUnrelatedEntries()
    {
        var cache = new CapabilityReportCache();
        var jump = Connection(null);
        var child = Connection(jump.Id);
        var grandchild = Connection(child.Id);
        var unrelated = Connection(null);
        var connections = new[] { grandchild, unrelated, child, jump };
        for (var i = 0; i < connections.Length; i++)
        {
            var report = Report(connections[i].Id);
            cache.Store(report);
        }

        cache.Invalidate(jump.Id, connections);

        cache.Find(jump.Id).Should().BeNull();
        cache.Find(child.Id).Should().BeNull();
        cache.Find(grandchild.Id).Should().BeNull();
        cache.Find(unrelated.Id).Should().NotBeNull();
    }

    [TestMethod]
    public void Clear_InvalidatesInFlightRefreshAsWellAsStoredReports()
    {
        var cache = new CapabilityReportCache();
        var id = Guid.NewGuid();
        var report = Report(id);
        cache.Store(report);
        var generation = cache.Generation;

        cache.Clear();

        cache.Find(report.ConnectionId).Should().BeNull();
        cache.Store(report, generation).Should().BeFalse();
    }

    private static CapabilityReport Report(Guid id)
    {
        return new CapabilityReport(id, DateTime.UtcNow, CapabilityReportState.Completed, []);
    }

    private static SavedConnection Connection(Guid? jump)
    {
        var id = Guid.NewGuid();
        var draft = new SavedConnectionDraft { Name = id.ToString(), Host = "example.test", UserName = "test", JumpHostId = jump };
        return new SavedConnection { Id = id, Details = draft };
    }

    private sealed class ManualClock : TimeProvider
    {
        private long _timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _timestamp;
        public void Advance(TimeSpan duration) => _timestamp += duration.Ticks;
    }
}
