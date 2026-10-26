using DTech.Core.Editor;
using DTech.Core.Files.Remote;
using FluentAssertions;

namespace DTech.Core.Tests.Editor;

[TestClass]
public sealed class EditorNamingTests
{
    private readonly AuxiliaryFileNames _names = new();
    private EditorPathResolver _paths = null!;

    [TestInitialize]
    public void Initialize()
    {
        var normalizer = new RemotePathNormalizer();
        _paths = new EditorPathResolver(normalizer);
    }

    [TestMethod]
    public void Backup_IsHiddenSiblingThatParsesBackToItsTime()
    {
        var createdAtUtc = new DateTime(2026, 3, 4, 5, 6, 7, 890, DateTimeKind.Utc);

        var name = _names.Backup("nginx.conf", createdAtUtc);
        var parsed = _names.ParseBackup("nginx.conf", name);

        name.Should().StartWith(".nginx.conf.termforge-").And.EndWith(".bak");
        parsed.Should().Be(createdAtUtc);
    }

    [TestMethod]
    public void ParseBackup_OfAnotherFile_ReturnsNull()
    {
        var createdAtUtc = new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);
        var name = _names.Backup("other.conf", createdAtUtc);

        var parsed = _names.ParseBackup("nginx.conf", name);

        parsed.Should().BeNull();
    }

    [TestMethod]
    public void ParseBackup_TemporaryFile_ReturnsNull()
    {
        var temporary = _names.Temporary("nginx.conf");

        var parsed = _names.ParseBackup("nginx.conf", temporary);

        parsed.Should().BeNull();
    }

    [TestMethod]
    public void Resolve_NormalPath_SplitsDirectoryAndName()
    {
        var location = _paths.Resolve("/etc//nginx/./nginx.conf");

        location.Value.Directory.Should().Be("/etc/nginx");
        location.Value.Name.Should().Be("nginx.conf");
        location.Value.Path.Should().Be("/etc/nginx/nginx.conf");
    }

    [TestMethod]
    public void Resolve_FileInRoot_BuildsRootSibling()
    {
        var location = _paths.Resolve("/notes.txt");

        location.Value.Sibling(".x").Should().Be("/.x");
    }

    [TestMethod]
    public void Resolve_Root_IsInvalid()
    {
        var location = _paths.Resolve("/");

        location.Error!.Code.Should().Be(EditorErrorCode.Invalid);
    }

    [TestMethod]
    public void Resolve_Traversal_IsInvalid()
    {
        var location = _paths.Resolve("/home/user/../../etc/shadow");

        location.Error!.Code.Should().Be(EditorErrorCode.Invalid);
    }
}
