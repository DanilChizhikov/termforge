using DTech.Core.Editor;
using DTech.Core.Files;
using FluentAssertions;

namespace DTech.Ssh.IntegrationTests.Editor;

[TestClass]
public sealed class RemoteEditorSymbolicLinkTests
{
    private const string Original = "original content";

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Open_SymbolicLink_IsRejectedWithoutReadingItsTarget(bool broken)
    {
        await using var session = await EditorSession.OpenAsync();
        var directory = await session.CreateWorkDirectoryAsync();
        var target = await session.CreateFileAsync(directory, "target.conf", Original);
        var link = $"{directory}/link.conf";
        var destination = broken ? $"{directory}/missing.conf" : target;
        await session.ExecuteAsync($"ln -s '{destination}' '{link}'");

        var result = await session.Documents.ReadAsync(session.Client, link, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        var expected = EditorError.SymbolicLink(link);
        result.Error.Should().Be(expected);
        var content = await session.ReadTextAsync(target);
        content.Should().Be(Original);
    }

    [TestMethod]
    public async Task BackupCatalog_ExcludesSymbolicLinksAndDirectories()
    {
        await using var session = await EditorSession.OpenAsync();
        var directory = await session.CreateWorkDirectoryAsync();
        var path = await session.CreateFileAsync(directory, "site.conf", Original);
        var target = await session.CreateFileAsync(directory, "target.conf", Original);
        const string regular = ".site.conf.termforge-20260104T000000000Z.bak";
        const string linked = ".site.conf.termforge-20260103T000000000Z.bak";
        const string broken = ".site.conf.termforge-20260102T000000000Z.bak";
        const string folder = ".site.conf.termforge-20260101T000000000Z.bak";
        await session.CreateFileAsync(directory, regular, Original);
        await session.ExecuteAsync($"ln -s '{target}' '{directory}/{linked}' && ln -s '{directory}/missing' '{directory}/{broken}' && mkdir '{directory}/{folder}'");
        var location = session.Locate(path);
        var limits = new EditorLimits(1024, 1);
        var pruner = new BackupPruner(session.Catalog, limits);

        var backups = await session.Catalog.ListAsync(session.Client, location, CancellationToken.None);
        var warnings = await pruner.PruneAsync(session.Client, location, CancellationToken.None);

        backups.IsSuccess.Should().BeTrue(backups.Error?.Message);
        backups.Value.Should().ContainSingle().Which.Name.Should().Be(regular);
        warnings.Should().BeEmpty();
        var content = await session.ReadTextAsync(target);
        content.Should().Be(Original);
        await session.ExecuteAsync($"test -L '{directory}/{linked}' && test -L '{directory}/{broken}' && test -d '{directory}/{folder}'");
    }

    [TestMethod]
    public async Task Delete_AfterListedRegularFileIsReplacedByLink_KeepsLinkTarget()
    {
        await using var session = await EditorSession.OpenAsync();
        var directory = await session.CreateWorkDirectoryAsync();
        var target = await session.CreateFileAsync(directory, "target.conf", Original);
        const string name = ".site.conf.termforge-20260101T000000000Z.bak";
        var backup = await session.CreateFileAsync(directory, name, "old backup");
        var listed = await session.Client.ListNamesAsync(directory, CancellationToken.None);
        listed.Value.Should().Contain(name);
        await session.ExecuteAsync($"rm -f '{backup}' && ln -s '{target}' '{backup}'");

        var deleted = await session.Client.DeleteFileAsync(backup, CancellationToken.None);

        deleted.IsSuccess.Should().BeTrue(deleted.Error?.Message);
        var content = await session.ReadTextAsync(target);
        content.Should().Be(Original);
        await session.ExecuteAsync($"test ! -e '{backup}' && test ! -L '{backup}'");
    }

    [TestMethod]
    public async Task Attributes_AfterListingAndReplacingFileByLink_UseFreshEntry()
    {
        await using var session = await EditorSession.OpenAsync();
        var directory = await session.CreateWorkDirectoryAsync();
        var target = await session.CreateFileAsync(directory, "target.conf", Original);
        var path = await session.CreateFileAsync(directory, "site.conf", "replace me");
        await session.Client.ListNamesAsync(directory, CancellationToken.None);
        await session.ExecuteAsync($"rm -f '{path}' && ln -s '{target}' '{path}'");

        var attributes = await session.Client.GetAttributesAsync(path, CancellationToken.None);
        var opened = await session.Documents.ReadAsync(session.Client, path, CancellationToken.None);

        attributes.IsSuccess.Should().BeTrue(attributes.Error?.Message);
        attributes.Value.Kind.Should().Be(RemoteFileKind.SymbolicLink);
        var expected = EditorError.SymbolicLink(path);
        opened.Error.Should().Be(expected);
    }
}
