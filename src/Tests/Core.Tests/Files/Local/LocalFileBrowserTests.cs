using System.Runtime.Versioning;
using DTech.Core.Files;
using FluentAssertions;

namespace DTech.Core.Tests.Files.Local;

[TestClass]
public sealed class LocalFileBrowserTests
{
    private LocalFileSystemFixture _fixture = null!;

    [TestInitialize]
    public void Initialize()
    {
        _fixture = new LocalFileSystemFixture();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _fixture.Dispose();
    }

    [TestMethod]
    public void List_ReturnsFilesAndDirectoriesWithMetadata()
    {
        _fixture.Directory.Write("notes.txt", "hello");
        _fixture.Directory.Write(".hidden", "x");
        Directory.CreateDirectory(_fixture.Directory.Combine("sub"));

        var result = _fixture.Browser.List(_fixture.Directory.Path);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        var listing = result.Value;
        listing.Path.Should().Be(_fixture.Directory.Path);
        listing.ParentPath.Should().Be(Path.GetDirectoryName(_fixture.Directory.Path));
        listing.Entries.Should().Contain(entry => entry.Name == "notes.txt" && entry.Kind == FileEntryKind.File && entry.Size == 5);
        listing.Entries.Should().Contain(entry => entry.Name == "sub" && entry.Kind == FileEntryKind.Directory);
        listing.Entries.Should().Contain(entry => entry.Name == ".hidden");
    }

    [TestMethod]
    public void List_FullPathOfEntry_PointsInsideListedDirectory()
    {
        _fixture.Directory.Write("a.txt", "a");

        var result = _fixture.Browser.List(_fixture.Directory.Path);

        var expected = _fixture.Directory.Combine("a.txt");
        result.Value.Entries.Should().ContainSingle().Which.Path.Should().Be(expected);
    }

    [TestMethod]
    public void List_WithoutPath_ListsHomeDirectory()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var result = _fixture.Browser.List(null);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.Path.Should().Be(Path.GetFullPath(home));
    }

    [TestMethod]
    public void List_MissingDirectory_ReturnsNotFound()
    {
        var missing = _fixture.Directory.Combine("missing");

        var result = _fixture.Browser.List(missing);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(FileErrorCode.NotFound);
    }

    [TestMethod]
    public void List_RegularFile_ReturnsNotADirectory()
    {
        var file = _fixture.Directory.Write("file.txt", "x");

        var result = _fixture.Browser.List(file);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(FileErrorCode.NotADirectory);
    }

    [TestMethod]
    public void List_PathWithParentSegment_IsRejectedBeforeTouchingDisk()
    {
        var traversal = Path.Combine(_fixture.Directory.Path, "..");

        var result = _fixture.Browser.List(traversal);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(FileErrorCode.Invalid);
    }

    [TestMethod]
    [UnsupportedOSPlatform("windows")]
    public void List_UnreadableDirectory_ReturnsPermissionDenied()
    {
        if (OperatingSystem.IsWindows() || Environment.IsPrivilegedProcess)
        {
            Assert.Inconclusive("Needs a non-privileged POSIX user.");
        }

        var locked = _fixture.Directory.Combine("locked");
        Directory.CreateDirectory(locked);
        File.SetUnixFileMode(locked, UnixFileMode.None);

        try
        {
            var result = _fixture.Browser.List(locked);

            result.IsSuccess.Should().BeFalse();
            result.Error!.Code.Should().Be(FileErrorCode.PermissionDenied);
        }
        finally
        {
            File.SetUnixFileMode(locked, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    [TestMethod]
    public void List_SymbolicLinkToDirectory_ReportsLinkWithTargetKind()
    {
        Directory.CreateDirectory(_fixture.Directory.Combine("real"));
        if (!_fixture.TryCreateSymbolicLink("link", _fixture.Directory.Combine("real")))
        {
            Assert.Inconclusive("Symbolic links are not available.");
        }

        var result = _fixture.Browser.List(_fixture.Directory.Path);

        result.Value.Entries.Should().Contain(entry => entry.Name == "link" && entry.Kind == FileEntryKind.SymbolicLink && entry.LinkTargetKind == FileEntryKind.Directory && !entry.IsBrokenLink);
    }

    [TestMethod]
    public void List_BrokenSymbolicLink_IsFlaggedAsBroken()
    {
        if (!_fixture.TryCreateSymbolicLink("dangling", _fixture.Directory.Combine("nowhere")))
        {
            Assert.Inconclusive("Symbolic links are not available.");
        }

        var result = _fixture.Browser.List(_fixture.Directory.Path);

        result.Value.Entries.Should().Contain(entry => entry.Name == "dangling" && entry.Kind == FileEntryKind.SymbolicLink && entry.IsBrokenLink);
    }

    [TestMethod]
    public void List_CircularSymbolicLink_FailsWithoutHanging()
    {
        var first = _fixture.Directory.Combine("loop-a");
        var second = _fixture.Directory.Combine("loop-b");
        var linked = _fixture.TryCreateSymbolicLink("loop-a", second) && _fixture.TryCreateSymbolicLink("loop-b", first);
        if (!linked)
        {
            Assert.Inconclusive("Symbolic links are not available.");
        }

        var result = _fixture.Browser.List(first);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(FileErrorCode.Invalid);
    }

    [TestMethod]
    public void List_ThroughSymbolicLinkToDirectory_ListsTargetContent()
    {
        _fixture.Directory.Write("real/inner.txt", "x");
        if (!_fixture.TryCreateSymbolicLink("link", _fixture.Directory.Combine("real")))
        {
            Assert.Inconclusive("Symbolic links are not available.");
        }

        var result = _fixture.Browser.List(_fixture.Directory.Combine("link"));

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.Entries.Should().ContainSingle(entry => entry.Name == "inner.txt");
    }

    [TestMethod]
    public void Stat_File_ReturnsMetadata()
    {
        var file = _fixture.Directory.Write("data.bin", "12345678");

        var result = _fixture.Browser.Stat(file);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.Kind.Should().Be(FileEntryKind.File);
        result.Value.Size.Should().Be(8);
        result.Value.ModifiedUtc.Should().NotBeNull();
    }

    [TestMethod]
    public void Stat_Missing_ReturnsNotFound()
    {
        var result = _fixture.Browser.Stat(_fixture.Directory.Combine("missing"));

        result.Error!.Code.Should().Be(FileErrorCode.NotFound);
    }

    [TestMethod]
    public void Stat_Traversal_IsRejected()
    {
        var traversal = $"{_fixture.Directory.Path}{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}x";

        var result = _fixture.Browser.Stat(traversal);

        result.Error!.Code.Should().Be(FileErrorCode.Invalid);
    }

    [TestMethod]
    public void GetRoots_IncludesHomeDirectory()
    {
        var roots = _fixture.Browser.GetRoots();

        roots.Should().NotBeEmpty();
        roots.Should().Contain(root => root.Name == "Home");
    }
}
