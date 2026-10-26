using DTech.Core.Files;
using FluentAssertions;

namespace DTech.Core.Tests.Files.Local;

[TestClass]
public sealed class LocalFileOperationsTests
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
    public void CreateDirectory_CreatesDirectoryInsideParent()
    {
        var request = new CreateDirectoryRequest(_fixture.Directory.Path, "fresh");

        var result = _fixture.Operations.CreateDirectory(request);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        Directory.Exists(_fixture.Directory.Combine("fresh")).Should().BeTrue();
    }

    [TestMethod]
    public void CreateDirectory_ExistingName_ReturnsAlreadyExists()
    {
        _fixture.Directory.Write("taken", "x");
        var request = new CreateDirectoryRequest(_fixture.Directory.Path, "taken");

        var result = _fixture.Operations.CreateDirectory(request);

        result.Error!.Code.Should().Be(FileErrorCode.AlreadyExists);
    }

    [TestMethod]
    [DataRow("../escape")]
    [DataRow("a/b")]
    [DataRow("..")]
    [DataRow("")]
    public void CreateDirectory_UnsafeName_IsRejected(string name)
    {
        var request = new CreateDirectoryRequest(_fixture.Directory.Path, name);

        var result = _fixture.Operations.CreateDirectory(request);

        result.Error!.Code.Should().Be(FileErrorCode.Invalid);
        Directory.GetFileSystemEntries(_fixture.Directory.Path).Should().BeEmpty();
    }

    [TestMethod]
    public void CreateDirectory_ParentWithTraversal_IsRejected()
    {
        var parent = Path.Combine(_fixture.Directory.Path, "..");
        var request = new CreateDirectoryRequest(parent, "x");

        var result = _fixture.Operations.CreateDirectory(request);

        result.Error!.Code.Should().Be(FileErrorCode.Invalid);
    }

    [TestMethod]
    public void Rename_File_MovesItWithinSameDirectory()
    {
        var source = _fixture.Directory.Write("old.txt", "content");
        var request = new RenameRequest(source, "new.txt");

        var result = _fixture.Operations.Rename(request);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        File.Exists(source).Should().BeFalse();
        File.ReadAllText(_fixture.Directory.Combine("new.txt")).Should().Be("content");
    }

    [TestMethod]
    public void Rename_Directory_Works()
    {
        var source = _fixture.Directory.Combine("dir");
        Directory.CreateDirectory(source);
        var request = new RenameRequest(source, "renamed");

        var result = _fixture.Operations.Rename(request);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        Directory.Exists(_fixture.Directory.Combine("renamed")).Should().BeTrue();
    }

    [TestMethod]
    public void Rename_ToExistingName_ReturnsAlreadyExistsAndKeepsBoth()
    {
        var source = _fixture.Directory.Write("a.txt", "a");
        var other = _fixture.Directory.Write("b.txt", "b");
        var request = new RenameRequest(source, "b.txt");

        var result = _fixture.Operations.Rename(request);

        result.Error!.Code.Should().Be(FileErrorCode.AlreadyExists);
        File.ReadAllText(source).Should().Be("a");
        File.ReadAllText(other).Should().Be("b");
    }

    [TestMethod]
    public void Rename_NewNameWithSeparator_CannotMoveFileElsewhere()
    {
        var source = _fixture.Directory.Write("a.txt", "a");
        var request = new RenameRequest(source, "../moved.txt");

        var result = _fixture.Operations.Rename(request);

        result.Error!.Code.Should().Be(FileErrorCode.Invalid);
        File.Exists(source).Should().BeTrue();
    }

    [TestMethod]
    public void Rename_Missing_ReturnsNotFound()
    {
        var request = new RenameRequest(_fixture.Directory.Combine("missing"), "x");

        var result = _fixture.Operations.Rename(request);

        result.Error!.Code.Should().Be(FileErrorCode.NotFound);
    }

    [TestMethod]
    public void Delete_WithoutConfirmation_IsRejectedAndKeepsFile()
    {
        var file = _fixture.Directory.Write("keep.txt", "x");
        var request = new DeleteRequest(file, false);

        var result = _fixture.Operations.Delete(request);

        result.Error!.Code.Should().Be(FileErrorCode.ConfirmationRequired);
        File.Exists(file).Should().BeTrue();
    }

    [TestMethod]
    public void Delete_WithConfirmation_RemovesFile()
    {
        var file = _fixture.Directory.Write("gone.txt", "x");
        var request = new DeleteRequest(file, true);

        var result = _fixture.Operations.Delete(request);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        File.Exists(file).Should().BeFalse();
    }

    [TestMethod]
    public void Delete_EmptyDirectory_RemovesIt()
    {
        var directory = _fixture.Directory.Combine("empty");
        Directory.CreateDirectory(directory);
        var request = new DeleteRequest(directory, true);

        var result = _fixture.Operations.Delete(request);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        Directory.Exists(directory).Should().BeFalse();
    }

    [TestMethod]
    public void Delete_NonEmptyDirectory_ReturnsNotEmptyAndKeepsContent()
    {
        var file = _fixture.Directory.Write("full/inner.txt", "x");
        var request = new DeleteRequest(_fixture.Directory.Combine("full"), true);

        var result = _fixture.Operations.Delete(request);

        result.Error!.Code.Should().Be(FileErrorCode.NotEmpty);
        File.Exists(file).Should().BeTrue();
    }

    [TestMethod]
    public void Delete_Missing_ReturnsNotFound()
    {
        var request = new DeleteRequest(_fixture.Directory.Combine("missing"), true);

        var result = _fixture.Operations.Delete(request);

        result.Error!.Code.Should().Be(FileErrorCode.NotFound);
    }

    [TestMethod]
    public void Delete_FilesystemRoot_IsRejected()
    {
        var root = Path.GetPathRoot(_fixture.Directory.Path)!;
        var request = new DeleteRequest(root, true);

        var result = _fixture.Operations.Delete(request);

        result.Error!.Code.Should().Be(FileErrorCode.Invalid);
    }

    [TestMethod]
    public void Delete_PathWithTraversal_IsRejected()
    {
        var file = _fixture.Directory.Write("victim.txt", "x");
        var traversal = Path.Combine(_fixture.Directory.Path, "sub", "..", "victim.txt");
        var request = new DeleteRequest(traversal, true);

        var result = _fixture.Operations.Delete(request);

        result.Error!.Code.Should().Be(FileErrorCode.Invalid);
        File.Exists(file).Should().BeTrue();
    }

    [TestMethod]
    public void Delete_SymbolicLinkToDirectory_RemovesOnlyTheLink()
    {
        var inner = _fixture.Directory.Write("real/inner.txt", "x");
        if (!_fixture.TryCreateSymbolicLink("link", _fixture.Directory.Combine("real")))
        {
            Assert.Inconclusive("Symbolic links are not available.");
        }

        var request = new DeleteRequest(_fixture.Directory.Combine("link"), true);

        var result = _fixture.Operations.Delete(request);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        File.Exists(inner).Should().BeTrue();
        new DirectoryInfo(_fixture.Directory.Combine("link")).LinkTarget.Should().BeNull();
        Directory.Exists(_fixture.Directory.Combine("link")).Should().BeFalse();
    }

    [TestMethod]
    public void Delete_BrokenSymbolicLink_RemovesIt()
    {
        if (!_fixture.TryCreateSymbolicLink("dangling", _fixture.Directory.Combine("nowhere")))
        {
            Assert.Inconclusive("Symbolic links are not available.");
        }

        var request = new DeleteRequest(_fixture.Directory.Combine("dangling"), true);

        var result = _fixture.Operations.Delete(request);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        Directory.GetFileSystemEntries(_fixture.Directory.Path).Should().BeEmpty();
    }
}
