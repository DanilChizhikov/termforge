using DTech.Core.Transfers.Execution;
using FluentAssertions;

namespace DTech.Core.Tests.Transfers;

[TestClass]
public sealed class TransferRateMeterTests
{
    [TestMethod]
    public void Sample_FirstSample_ReportsNoRate()
    {
        var meter = new TransferRateMeter();

        var rate = meter.Sample(TimeSpan.Zero, 0, 1000);

        rate.BytesPerSecond.Should().Be(0);
        rate.RemainingSeconds.Should().BeNull();
    }

    [TestMethod]
    public void Sample_SteadyProgress_ReportsRateAndRemainingTime()
    {
        var meter = new TransferRateMeter();
        meter.Sample(TimeSpan.Zero, 0, 1000);

        var rate = meter.Sample(TimeSpan.FromSeconds(2), 200, 1000);

        rate.BytesPerSecond.Should().BeApproximately(100, 0.001);
        rate.RemainingSeconds.Should().BeApproximately(8, 0.001);
    }

    [TestMethod]
    public void Sample_ResumedTransfer_MeasuresOnlyNewBytes()
    {
        var meter = new TransferRateMeter();
        meter.Sample(TimeSpan.Zero, 500, 1000);

        var rate = meter.Sample(TimeSpan.FromSeconds(1), 600, 1000);

        rate.BytesPerSecond.Should().BeApproximately(100, 0.001);
        rate.RemainingSeconds.Should().BeApproximately(4, 0.001);
    }

    [TestMethod]
    public void Sample_StalledBeyondWindow_DropsToZero()
    {
        var meter = new TransferRateMeter(TimeSpan.FromSeconds(5));
        meter.Sample(TimeSpan.Zero, 0, 1000);
        meter.Sample(TimeSpan.FromSeconds(1), 100, 1000);
        meter.Sample(TimeSpan.FromSeconds(7), 100, 1000);

        var rate = meter.Sample(TimeSpan.FromSeconds(13), 100, 1000);

        rate.BytesPerSecond.Should().Be(0);
        rate.RemainingSeconds.Should().BeNull();
    }
}
