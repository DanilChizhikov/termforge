using DTech.Core.Files;
using FluentAssertions;

namespace DTech.Core.Tests.Files.Local;

[TestClass]
public sealed class LocalCaseRenameTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Rename_OnlyCase_ChangesStoredNameAndKeepsContent(bool isDirectory)
    {
        using var fixture = new LocalFileSystemFixture();
        var sourcePath = CreateSource(fixture, isDirectory);
        var request = new RenameRequest(sourcePath, "README");

        var result = fixture.Operations.Rename(request);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        var names = Directory.GetFileSystemEntries(fixture.Directory.Path).Select(Path.GetFileName);
        names.Should().Contain("README").And.NotContain("readme");
        var targetPath = fixture.Directory.Combine("README");
        var contentPath = isDirectory ? Path.Combine(targetPath, "content.txt") : targetPath;
        File.ReadAllText(contentPath).Should().Be("original");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Rename_OnlyCase_WithSeparateTarget_KeepsBoth(bool isDirectory)
    {
        using var fixture = new LocalFileSystemFixture();
        var sourcePath = CreateSource(fixture, isDirectory);
        var targetPath = fixture.Directory.Combine("README");
        if (File.Exists(targetPath) || Directory.Exists(targetPath))
        {
            Assert.Inconclusive("This filesystem does not support distinct case variants.");
        }

        File.WriteAllText(targetPath, "existing target");
        var request = new RenameRequest(sourcePath, "README");

        var result = fixture.Operations.Rename(request);

        result.Error!.Code.Should().Be(FileErrorCode.AlreadyExists);
        var contentPath = isDirectory ? Path.Combine(sourcePath, "content.txt") : sourcePath;
        File.ReadAllText(contentPath).Should().Be("original");
        File.ReadAllText(targetPath).Should().Be("existing target");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Rename_ExactName_ReturnsAlreadyExists(bool isDirectory)
    {
        using var fixture = new LocalFileSystemFixture();
        var sourcePath = CreateSource(fixture, isDirectory);
        var request = new RenameRequest(sourcePath, "readme");

        var result = fixture.Operations.Rename(request);

        result.Error!.Code.Should().Be(FileErrorCode.AlreadyExists);
        var names = Directory.GetFileSystemEntries(fixture.Directory.Path).Select(Path.GetFileName);
        names.Should().Equal("readme");
    }

    [TestMethod]
    public void Rename_BrokenSymbolicLink_OnlyCase_KeepsLinkTarget()
    {
        using var fixture = new LocalFileSystemFixture();
        var targetPath = fixture.Directory.Combine("missing");
        if (!fixture.TryCreateSymbolicLink("readme", targetPath))
        {
            Assert.Inconclusive("Symbolic links are not available.");
        }

        var sourcePath = fixture.Directory.Combine("readme");
        var request = new RenameRequest(sourcePath, "README");

        var result = fixture.Operations.Rename(request);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        var names = Directory.GetFileSystemEntries(fixture.Directory.Path).Select(Path.GetFileName);
        names.Should().Contain("README").And.NotContain("readme");
        var renamedPath = fixture.Directory.Combine("README");
        var renamed = new FileInfo(renamedPath);
        renamed.LinkTarget.Should().Be(targetPath);
        File.Exists(targetPath).Should().BeFalse();
    }

    private static string CreateSource(LocalFileSystemFixture fixture, bool isDirectory)
    {
        if (!isDirectory)
        {
            return fixture.Directory.Write("readme", "original");
        }

        fixture.Directory.Write("readme/content.txt", "original");
        return fixture.Directory.Combine("readme");
    }
}
