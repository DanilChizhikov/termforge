using DTech.Core.Docker;
using FluentAssertions;

namespace DTech.Core.Tests.Docker;

[TestClass]
public sealed class ContainerReferenceTests
{
    [TestMethod]
    [DataRow("web")]
    [DataRow("worker.paused")]
    [DataRow("my_app-1")]
    [DataRow("3f4e1b2c9d8a")]
    [DataRow("3f4e1b2c9d8a7f6e5d4c3b2a1f0e9d8c7b6a5f4e3d2c1b0a9f8e7d6c5b4a3f2e")]
    public void TryParse_AcceptsDockerNamesAndIds(string candidate)
    {
        var parsed = ContainerReference.TryParse(candidate, out var reference);

        parsed.Should().BeTrue();
        reference.Value.Should().Be(candidate);
        reference.Quoted.Should().Be($"'{candidate}'");
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" web")]
    [DataRow("web ")]
    [DataRow("-rm")]
    [DataRow(".hidden")]
    [DataRow("web;rm -rf /")]
    [DataRow("$(id)")]
    [DataRow("`id`")]
    [DataRow("web'||'id")]
    [DataRow("web\nid")]
    [DataRow("web\n")]
    [DataRow("web/../etc")]
    [DataRow("wеb")]
    public void TryParse_RejectsInjectionAttemptsAndInvalidNames(string? candidate)
    {
        var parsed = ContainerReference.TryParse(candidate, out _);

        parsed.Should().BeFalse();
    }

    [TestMethod]
    public void TryParse_RejectsNamesLongerThan128Characters()
    {
        var longest = new string('a', 128);
        var tooLong = new string('a', 129);

        var longestParsed = ContainerReference.TryParse(longest, out _);
        var tooLongParsed = ContainerReference.TryParse(tooLong, out _);

        longestParsed.Should().BeTrue();
        tooLongParsed.Should().BeFalse();
    }
}
