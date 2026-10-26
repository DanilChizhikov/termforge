using DTech.Core.Reconnect;
using FluentAssertions;

namespace DTech.Core.Tests.Reconnect;

[TestClass]
public sealed class ReconnectBackoffTests
{
    private static readonly ReconnectPolicy Policy = new(ReconnectMode.Automatic, 6, 2, 20);

    [TestMethod]
    public void DelayBefore_DoublesEachAttemptStartingFromInitialDelay()
    {
        var delays = Enumerable.Range(1, 4).Select(attempt => ReconnectBackoff.DelayBefore(Policy, attempt)).ToList();

        delays.Should().Equal(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(16));
    }

    [TestMethod]
    public void DelayBefore_NeverExceedsMaxDelay()
    {
        var fifth = ReconnectBackoff.DelayBefore(Policy, 5);
        var huge = ReconnectBackoff.DelayBefore(Policy, 500);

        fifth.Should().Be(TimeSpan.FromSeconds(20));
        huge.Should().Be(TimeSpan.FromSeconds(20));
    }

    [TestMethod]
    public void DelayBefore_ForAttemptBelowOne_IsZero()
    {
        ReconnectBackoff.DelayBefore(Policy, 0).Should().Be(TimeSpan.Zero);
    }
}
