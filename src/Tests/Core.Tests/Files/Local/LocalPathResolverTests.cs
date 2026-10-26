using DTech.Core.Files;
using DTech.Core.Files.Local;
using DTech.Core.Tests.SavedConnections.Support;
using FluentAssertions;

namespace DTech.Core.Tests.Files.Local;

[TestClass]
public sealed class LocalPathResolverTests
{
    private TemporaryDirectory _directory = null!;
    private LocalPathResolver _resolver = null!;

    [TestInitialize]
    public void Initialize()
    {
        _directory = new TemporaryDirectory();
        _resolver = new LocalPathResolver();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _directory.Dispose();
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void Resolve_EmptyPath_IsRejected(string? path)
    {
        var result = _resolver.Resolve(path);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(FileErrorCode.Invalid);
    }

    [TestMethod]
    [DataRow("relative/path")]
    [DataRow("./here")]
    [DataRow("file.txt")]
    public void Resolve_RelativePath_IsRejected(string path)
    {
        var result = _resolver.Resolve(path);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(FileErrorCode.Invalid);
    }

    [TestMethod]
    public void Resolve_ParentSegmentInMiddle_IsRejected()
    {
        var path = $"{_directory.Path}{Path.DirectorySeparatorChar}sub{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}other";

        var result = _resolver.Resolve(path);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Should().Contain("..");
    }

    [TestMethod]
    public void Resolve_TrailingParentSegment_IsRejected()
    {
        var path = $"{_directory.Path}{Path.DirectorySeparatorChar}..";

        var result = _resolver.Resolve(path);

        result.IsSuccess.Should().BeFalse();
    }

    [TestMethod]
    public void Resolve_ParentSegmentWithForeignSeparator_IsRejected()
    {
        var path = $"{_directory.Path}/sub/../../etc";

        var result = _resolver.Resolve(path);

        result.IsSuccess.Should().BeFalse();
    }

    [TestMethod]
    public void Resolve_NullCharacter_IsRejected()
    {
        var path = $"{_directory.Path}{Path.DirectorySeparatorChar}file\0.txt";

        var result = _resolver.Resolve(path);

        result.IsSuccess.Should().BeFalse();
    }

    [TestMethod]
    [DataRow(@"\\?\C:\Windows")]
    [DataRow(@"\\.\PhysicalDrive0")]
    public void Resolve_DeviceNamespace_IsRejected(string path)
    {
        var result = _resolver.Resolve(path);

        result.IsSuccess.Should().BeFalse();
    }

    [TestMethod]
    public void Resolve_AbsolutePathWithRedundantSegments_IsNormalized()
    {
        var separator = Path.DirectorySeparatorChar;
        var messy = $"{_directory.Path}{separator}.{separator}sub{separator}{separator}leaf{separator}";

        var result = _resolver.Resolve(messy);

        result.IsSuccess.Should().BeTrue();
        var expected = Path.Combine(_directory.Path, "sub", "leaf");
        result.Value.Should().Be(expected);
    }
}
