using DTech.Core.Updates;
using FluentAssertions;

namespace DTech.Core.Tests.Updates;

[TestClass]
public sealed class SemanticVersionTests
{
    [TestMethod]
    [DataRow("1.2.3", "1.2.3")]
    [DataRow("v1.2.3", "1.2.3")]
    [DataRow("V0.1.0", "0.1.0")]
    [DataRow("1.2.3+45", "1.2.3")]
    [DataRow("0.1.0+0", "0.1.0")]
    [DataRow("1.0.0-rc.1", "1.0.0-rc.1")]
    [DataRow("1.0.0-rc.1+build.5", "1.0.0-rc.1")]
    [DataRow(" 2.0.0 ", "2.0.0")]
    public void TryParse_accepts_valid_versions(string text, string expected)
    {
        var parsed = SemanticVersion.TryParse(text, out var version);

        parsed.Should().BeTrue();
        version!.ToString().Should().Be(expected);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("1.2")]
    [DataRow("1.2.3.4")]
    [DataRow("latest")]
    [DataRow("1.2.x")]
    [DataRow("-1.2.3")]
    [DataRow("1.2.3-")]
    [DataRow("99999999999.0.0")]
    public void TryParse_rejects_invalid_versions(string? text)
    {
        var parsed = SemanticVersion.TryParse(text, out var version);

        parsed.Should().BeFalse();
        version.Should().BeNull();
    }

    [TestMethod]
    [DataRow("1.0.0", "2.0.0")]
    [DataRow("1.9.0", "1.10.0")]
    [DataRow("1.0.9", "1.0.10")]
    [DataRow("1.0.0-alpha", "1.0.0")]
    [DataRow("1.0.0-alpha", "1.0.0-alpha.1")]
    [DataRow("1.0.0-alpha.1", "1.0.0-alpha.beta")]
    [DataRow("1.0.0-alpha.beta", "1.0.0-beta")]
    [DataRow("1.0.0-beta.2", "1.0.0-beta.11")]
    [DataRow("1.0.0-beta.11", "1.0.0-rc.1")]
    public void CompareTo_orders_lower_before_higher(string lower, string higher)
    {
        SemanticVersion.TryParse(lower, out var lowerVersion);
        SemanticVersion.TryParse(higher, out var higherVersion);

        lowerVersion!.CompareTo(higherVersion).Should().BeNegative();
        higherVersion!.CompareTo(lowerVersion).Should().BePositive();
    }

    [TestMethod]
    public void CompareTo_ignores_build_metadata()
    {
        SemanticVersion.TryParse("1.0.0+1", out var first);
        SemanticVersion.TryParse("1.0.0+2", out var second);

        first!.CompareTo(second).Should().Be(0);
        first.Equals(second).Should().BeTrue();
        first.GetHashCode().Should().Be(second!.GetHashCode());
    }
}
