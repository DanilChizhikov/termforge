using DTech.Core.Files;
using DTech.Core.Files.Remote;
using FluentAssertions;

namespace DTech.Core.Tests.Files.Remote;

[TestClass]
public sealed class RemotePathNormalizerTests
{
    private readonly RemotePathNormalizer _normalizer = new();

    [TestMethod]
    [DataRow("/", "/")]
    [DataRow("/home/user", "/home/user")]
    [DataRow("/home//user/", "/home/user")]
    [DataRow("/home/./user", "/home/user")]
    [DataRow("///", "/")]
    public void Normalize_ProducesCanonicalPath(string raw, string expected)
    {
        var result = _normalizer.Normalize(raw);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expected);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("relative/path")]
    [DataRow("~/x")]
    [DataRow("/home/../etc")]
    [DataRow("/..")]
    [DataRow("/a/b/..")]
    [DataRow("/bad\0path")]
    [DataRow("/bad\npath")]
    public void Normalize_RejectsUnsafePaths(string? raw)
    {
        var result = _normalizer.Normalize(raw);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(FileErrorCode.Invalid);
    }

    [TestMethod]
    [DataRow("/", null)]
    [DataRow("/home", "/")]
    [DataRow("/home/user", "/home")]
    public void GetParent_ReturnsParentOrNullForRoot(string path, string? expected)
    {
        var parent = _normalizer.GetParent(path);

        parent.Should().Be(expected);
    }

    [TestMethod]
    [DataRow("/", "etc", "/etc")]
    [DataRow("/home", "user", "/home/user")]
    public void Combine_JoinsWithoutDoubleSeparator(string parent, string name, string expected)
    {
        var combined = _normalizer.Combine(parent, name);

        combined.Should().Be(expected);
    }
}
