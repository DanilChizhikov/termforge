using DTech.Entrypoint.Security.Network;
using DTech.Entrypoint.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace DTech.Entrypoint.Tests.Security.Network;

[TestClass]
public sealed class LoginAttemptLimiterTests
{
    private const string FirstClient = "192.168.1.20";
    private const string SecondClient = "192.168.1.21";

    private static readonly DateTimeOffset StartTime = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly MovableTimeProvider _time = new(StartTime);

    [TestMethod]
    public void Client_is_locked_out_after_maximum_attempts()
    {
        var limiter = CreateLimiter(3, 100);

        var firstDecision = limiter.TryBegin(FirstClient);
        var secondDecision = limiter.TryBegin(FirstClient);
        var thirdDecision = limiter.TryBegin(FirstClient);
        var lockedDecision = limiter.TryBegin(FirstClient);

        firstDecision.IsAllowed.Should().BeTrue();
        secondDecision.IsAllowed.Should().BeTrue();
        thirdDecision.IsAllowed.Should().BeTrue();
        lockedDecision.IsAllowed.Should().BeFalse();
        var lockoutDuration = TimeSpan.FromMinutes(15);
        lockedDecision.RetryAfter.Should().Be(lockoutDuration);
    }

    [TestMethod]
    public void Lockout_ends_after_lockout_duration()
    {
        var limiter = CreateLimiter(1, 100);
        limiter.TryBegin(FirstClient);
        var elapsedBeforeRetry = TimeSpan.FromMinutes(14);
        var expectedRetryAfter = TimeSpan.FromMinutes(1);

        _time.Advance(elapsedBeforeRetry);
        var stillLocked = limiter.TryBegin(FirstClient);
        _time.Advance(expectedRetryAfter);
        var unlocked = limiter.TryBegin(FirstClient);

        stillLocked.IsAllowed.Should().BeFalse();
        stillLocked.RetryAfter.Should().Be(expectedRetryAfter);
        unlocked.IsAllowed.Should().BeTrue();
    }

    [TestMethod]
    public void Success_resets_failed_attempts()
    {
        var limiter = CreateLimiter(2, 100);
        limiter.TryBegin(FirstClient);
        limiter.RecordSuccess(FirstClient);

        var afterSuccess = limiter.TryBegin(FirstClient);
        var stillAllowed = limiter.TryBegin(SecondClient);

        afterSuccess.IsAllowed.Should().BeTrue();
        stillAllowed.IsAllowed.Should().BeTrue();
    }

    [TestMethod]
    public void Old_failures_expire_after_lockout_duration()
    {
        var limiter = CreateLimiter(2, 100);
        limiter.TryBegin(FirstClient);
        _time.Advance(TimeSpan.FromMinutes(16));

        var firstAfterExpiry = limiter.TryBegin(FirstClient);
        var secondAfterExpiry = limiter.TryBegin(FirstClient);
        var thirdAfterExpiry = limiter.TryBegin(FirstClient);

        firstAfterExpiry.IsAllowed.Should().BeTrue();
        secondAfterExpiry.IsAllowed.Should().BeTrue();
        thirdAfterExpiry.IsAllowed.Should().BeFalse();
    }

    [TestMethod]
    public void Clients_are_limited_independently()
    {
        var limiter = CreateLimiter(1, 100);
        limiter.TryBegin(FirstClient);

        var secondClientDecision = limiter.TryBegin(SecondClient);
        var firstClientDecision = limiter.TryBegin(FirstClient);

        secondClientDecision.IsAllowed.Should().BeTrue();
        firstClientDecision.IsAllowed.Should().BeFalse();
    }

    [TestMethod]
    public void Global_limit_locks_out_every_client()
    {
        var limiter = CreateLimiter(10, 3);
        limiter.TryBegin("10.0.0.1");
        limiter.TryBegin("10.0.0.2");
        limiter.TryBegin("10.0.0.3");

        var newClientDecision = limiter.TryBegin("10.0.0.4");

        newClientDecision.IsAllowed.Should().BeFalse();
    }

    private LoginAttemptLimiter CreateLimiter(int maxAttempts, int globalMaxAttempts)
    {
        var networkAccess = new NetworkAccessOptions
        {
            MaxFailedLoginAttempts = maxAttempts,
            GlobalMaxFailedLoginAttempts = globalMaxAttempts,
            LockoutMinutes = 15,
        };
        var options = Options.Create(networkAccess);
        return new LoginAttemptLimiter(options, _time);
    }
}
