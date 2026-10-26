using DTech.Core.Reconnect;
using FluentAssertions;

namespace DTech.Core.Tests.Reconnect;

[TestClass]
public sealed class ReconnectPolicyValidatorTests
{
    [TestMethod]
    public void Validate_DefaultPolicy_IsValid()
    {
        ReconnectPolicyValidator.Validate(ReconnectPolicy.Default).Should().BeNull();
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(21)]
    [DataRow(-1)]
    public void Validate_AttemptLimitOutOfRange_IsRejected(int attempts)
    {
        var policy = ReconnectPolicy.Default with { MaxAttempts = attempts };

        ReconnectPolicyValidator.Validate(policy).Should().Contain("attempt limit");
    }

    [TestMethod]
    [DataRow(0, 60)]
    [DataRow(2, 3601)]
    public void Validate_DelayOutOfRange_IsRejected(int initial, int maximum)
    {
        var policy = ReconnectPolicy.Default with { InitialDelaySeconds = initial, MaxDelaySeconds = maximum };

        ReconnectPolicyValidator.Validate(policy).Should().Contain("delays");
    }

    [TestMethod]
    public void Validate_MaxDelayShorterThanInitial_IsRejected()
    {
        var policy = ReconnectPolicy.Default with { InitialDelaySeconds = 30, MaxDelaySeconds = 10 };

        ReconnectPolicyValidator.Validate(policy).Should().Contain("maximum");
    }

    [TestMethod]
    public void Validate_UnknownMode_IsRejected()
    {
        var policy = ReconnectPolicy.Default with { Mode = (ReconnectMode)99 };

        ReconnectPolicyValidator.Validate(policy).Should().Contain("mode");
    }
}
