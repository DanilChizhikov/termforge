using DTech.Core.Updates;
using FluentAssertions;
using NSubstitute;

namespace DTech.Core.Tests.Updates;

[TestClass]
public sealed class UpdateCheckerTests
{
    private static readonly DateTimeOffset StartTime = new(2026, 10, 25, 10, 0, 0, TimeSpan.Zero);
    private static readonly UpdateCacheSettings CacheSettings = new(TimeSpan.FromHours(6), TimeSpan.FromMinutes(5));

    private IReleaseSource _source = null!;
    private MovableTimeProvider _time = null!;
    private UpdateChecker _checker = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Initialize()
    {
        _source = Substitute.For<IReleaseSource>();
        _time = new MovableTimeProvider(StartTime);
        var versionProvider = Substitute.For<ICurrentVersionProvider>();
        SemanticVersion.TryParse("0.1.0", out var current);
        versionProvider.Current.Returns(current!);
        _checker = new UpdateChecker(_source, versionProvider, _time, CacheSettings);
    }

    [TestMethod]
    public void Peek_before_any_check_is_not_checked()
    {
        var status = _checker.Peek();

        status.State.Should().Be(UpdateState.NotChecked);
        status.CurrentVersion.Should().Be("0.1.0");
        _source.DidNotReceiveWithAnyArgs().GetLatestRelease(default);
    }

    [TestMethod]
    public async Task Check_reports_update_when_newer_release_exists()
    {
        ReturnRelease("0.2.0");

        var status = await _checker.Check(false, TestContext.CancellationToken);

        status.State.Should().Be(UpdateState.UpdateAvailable);
        status.LatestVersion.Should().Be("0.2.0");
        status.ReleaseUrl.Should().Be("https://github.com/DanilChizhikov/termforge/releases/tag/v0.2.0");
        status.CheckedAt.Should().Be(StartTime.UtcDateTime);
    }

    [TestMethod]
    [DataRow("0.1.0")]
    [DataRow("0.0.9")]
    public async Task Check_reports_up_to_date_when_release_is_not_newer(string releaseVersion)
    {
        ReturnRelease(releaseVersion);

        var status = await _checker.Check(false, TestContext.CancellationToken);

        status.State.Should().Be(UpdateState.UpToDate);
        status.ReleaseUrl.Should().BeNull();
    }

    [TestMethod]
    public async Task Check_reports_up_to_date_when_no_release_is_published()
    {
        _source.GetLatestRelease(Arg.Any<CancellationToken>()).Returns(ReleaseLookup.NoRelease);

        var status = await _checker.Check(false, TestContext.CancellationToken);

        status.State.Should().Be(UpdateState.UpToDate);
        status.LatestVersion.Should().BeNull();
    }

    [TestMethod]
    public async Task Check_reports_failure_quietly_when_lookup_fails()
    {
        _source.GetLatestRelease(Arg.Any<CancellationToken>()).Returns(ReleaseLookup.Failed);

        var status = await _checker.Check(false, TestContext.CancellationToken);

        status.State.Should().Be(UpdateState.CheckFailed);
        status.CurrentVersion.Should().Be("0.1.0");
    }

    [TestMethod]
    public async Task Check_reports_failure_quietly_when_source_throws()
    {
        _source.GetLatestRelease(Arg.Any<CancellationToken>()).Returns<Task<ReleaseLookup>>(_ => throw new InvalidOperationException("boom"));

        var status = await _checker.Check(false, TestContext.CancellationToken);

        status.State.Should().Be(UpdateState.CheckFailed);
    }

    [TestMethod]
    public async Task Check_propagates_cancellation()
    {
        _source.GetLatestRelease(Arg.Any<CancellationToken>()).Returns<Task<ReleaseLookup>>(_ => throw new OperationCanceledException());

        var act = () => _checker.Check(false, TestContext.CancellationToken);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [TestMethod]
    public async Task Check_uses_cache_until_it_expires()
    {
        ReturnRelease("0.2.0");

        await _checker.Check(false, TestContext.CancellationToken);
        _time.Advance(TimeSpan.FromHours(5));
        await _checker.Check(false, TestContext.CancellationToken);
        _time.Advance(TimeSpan.FromHours(2));
        await _checker.Check(false, TestContext.CancellationToken);

        await _source.Received(2).GetLatestRelease(Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task Check_force_bypasses_cache()
    {
        ReturnRelease("0.2.0");

        await _checker.Check(false, TestContext.CancellationToken);
        await _checker.Check(true, TestContext.CancellationToken);

        await _source.Received(2).GetLatestRelease(Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task Check_retries_failures_after_short_cache()
    {
        _source.GetLatestRelease(Arg.Any<CancellationToken>()).Returns(ReleaseLookup.Failed);

        await _checker.Check(false, TestContext.CancellationToken);
        _time.Advance(TimeSpan.FromMinutes(1));
        await _checker.Check(false, TestContext.CancellationToken);
        _time.Advance(TimeSpan.FromMinutes(5));
        await _checker.Check(false, TestContext.CancellationToken);

        await _source.Received(2).GetLatestRelease(Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task Peek_returns_last_status_after_check()
    {
        ReturnRelease("0.2.0");

        await _checker.Check(false, TestContext.CancellationToken);
        var status = _checker.Peek();

        status.State.Should().Be(UpdateState.UpdateAvailable);
    }

    [TestMethod]
    public async Task Concurrent_checks_share_one_lookup()
    {
        ReturnRelease("0.2.0");

        var first = _checker.Check(false, TestContext.CancellationToken);
        var second = _checker.Check(false, TestContext.CancellationToken);
        await Task.WhenAll(first, second);

        await _source.Received(1).GetLatestRelease(Arg.Any<CancellationToken>());
    }

    private void ReturnRelease(string version)
    {
        SemanticVersion.TryParse(version, out var parsed);
        var release = new ReleaseInfo(parsed!, "https://github.com/DanilChizhikov/termforge/releases/tag/v0.2.0");
        _source.GetLatestRelease(Arg.Any<CancellationToken>()).Returns(ReleaseLookup.Found(release));
    }

    private sealed class MovableTimeProvider : TimeProvider
    {
        private DateTimeOffset _now;

        public MovableTimeProvider(DateTimeOffset now)
        {
            _now = now;
        }

        public void Advance(TimeSpan duration)
        {
            _now += duration;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _now;
        }
    }
}
