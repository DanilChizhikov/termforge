using DTech.Core.Docker;
using FluentAssertions;

namespace DTech.Core.Tests.Docker;

[TestClass]
public sealed class DockerReferenceValidationTests
{
    [TestMethod]
    [DataRow("nginx")]
    [DataRow("nginx:1.27-alpine")]
    [DataRow("ghcr.io/acme/api:1.2")]
    [DataRow("registry.example.test:5000/team/app@sha256:0123abcd")]
    [DataRow("sha256:3f4e1b2c9d8a")]
    public void ImageReference_AcceptsRegistryReferences(string candidate)
    {
        ImageReference.TryParse(candidate, out var reference).Should().BeTrue();
        reference.Value.Should().Be(candidate);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("-nginx")]
    [DataRow("nginx;reboot")]
    [DataRow("nginx $(id)")]
    [DataRow("../etc/passwd")]
    [DataRow("a/../b")]
    public void ImageReference_RejectsUnsafeValues(string? candidate)
    {
        ImageReference.TryParse(candidate, out _).Should().BeFalse();
    }

    [TestMethod]
    [DataRow("shop", true)]
    [DataRow("my_app-2", true)]
    [DataRow("Shop", false)]
    [DataRow("-shop", false)]
    [DataRow("shop;id", false)]
    [DataRow(null, false)]
    public void ComposeProjectName_FollowsComposeNamingRules(string? candidate, bool expected)
    {
        ComposeProjectName.TryParse(candidate, out _).Should().Be(expected);
    }
}
