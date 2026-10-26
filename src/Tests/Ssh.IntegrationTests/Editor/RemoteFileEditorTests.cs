using System.Security.Cryptography;
using System.Text;
using DTech.Core.Editor;
using DTech.Ssh.IntegrationTests.Harness;
using FluentAssertions;

namespace DTech.Ssh.IntegrationTests.Editor;

[TestClass]
public sealed class RemoteFileEditorTests
{
    private const string Original = "server {\n  listen 80;\n}\n";
    private const string Edited = "server {\n  listen 8080;\n}\n";

    [TestMethod]
    public async Task Save_RoundTrip_PersistsContentAndReturnsMatchingVersion()
    {
        await using var session = await EditorSession.OpenAsync();
        var directory = await session.CreateWorkDirectoryAsync();
        var path = await session.CreateFileAsync(directory, "site.conf", Original);
        var version = await session.OpenVersionAsync(path);

        var result = await session.SaveAsync(path, Edited, version);
        var reopened = await session.Documents.ReadAsync(session.Client, path, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.Saved!.Strategy.Should().Be(SaveStrategy.AtomicRename);
        reopened.Value.Content.Text.Should().Be(Edited);
        var editedBytes = Encoding.UTF8.GetBytes(Edited);
        var editedHash = SHA256.HashData(editedBytes);
        var expectedHash = Convert.ToHexStringLower(editedHash);
        reopened.Value.Version.Sha256.Should().Be(expectedHash);
        reopened.Value.Version.Should().Be(result.Value.Saved.Version);
    }

    [TestMethod]
    public async Task Save_AfterExternalModification_ReturnsConflictAndKeepsServerContent()
    {
        await using var session = await EditorSession.OpenAsync();
        var directory = await session.CreateWorkDirectoryAsync();
        var path = await session.CreateFileAsync(directory, "site.conf", Original);
        var version = await session.OpenVersionAsync(path);
        await session.ExecuteAsync($"printf 'changed elsewhere\\n' > '{path}'");

        var result = await session.SaveAsync(path, Edited, version);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.IsSaved.Should().BeFalse();
        result.Value.Conflict!.CurrentContent!.Value.Text.Should().Be("changed elsewhere\n");
        var serverContent = await session.ReadTextAsync(path);
        serverContent.Should().Be("changed elsewhere\n");
    }

    [TestMethod]
    public async Task Save_CreatesBackupOfOriginalAndLeavesNoTemporaryFiles()
    {
        await using var session = await EditorSession.OpenAsync();
        var directory = await session.CreateWorkDirectoryAsync();
        var path = await session.CreateFileAsync(directory, "site.conf", Original);
        var version = await session.OpenVersionAsync(path);

        var result = await session.SaveAsync(path, Edited, version);
        var listing = await session.ExecuteAsync($"ls -A '{directory}'");

        var backup = result.Value.Saved!.Backup!;
        var backupContent = await session.ReadTextAsync(backup.Path);
        backupContent.Should().Be(Original);
        var names = listing.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        names.Should().BeEquivalentTo("site.conf", backup.Name);
    }

    [TestMethod]
    public async Task Save_WhenDirectoryIsNotWritable_FailsAndKeepsOriginal()
    {
        await using var session = await EditorSession.OpenAsync();
        var directory = await session.CreateWorkDirectoryAsync();
        var path = await session.CreateFileAsync(directory, "site.conf", Original);
        var version = await session.OpenVersionAsync(path);
        await session.ExecuteAsync($"chmod 555 '{directory}'");

        try
        {
            var result = await session.SaveAsync(path, Edited, version);

            result.IsSuccess.Should().BeFalse();
            result.Error!.Code.Should().Be(EditorErrorCode.PermissionDenied);
            result.Error.Message.Should().Contain("was not changed");
            var serverContent = await session.ReadTextAsync(path);
            serverContent.Should().Be(Original);
            var listing = await session.ExecuteAsync($"ls -A '{directory}'");
            listing.Should().Be("site.conf");
        }
        finally
        {
            await session.ExecuteAsync($"chmod 755 '{directory}'");
        }
    }

    [TestMethod]
    public async Task Rollback_RestoresBackupAndBacksUpReplacedContent()
    {
        await using var session = await EditorSession.OpenAsync();
        var directory = await session.CreateWorkDirectoryAsync();
        var path = await session.CreateFileAsync(directory, "site.conf", Original);
        var version = await session.OpenVersionAsync(path);
        var saved = await session.SaveAsync(path, Edited, version);
        var location = session.Locate(path);
        var request = new RollbackRequest(location, saved.Value.Saved!.Backup!.Name, saved.Value.Saved.Version);

        var rolledBack = await session.Rollback.RollbackAsync(session.Client, request, CancellationToken.None);
        var backups = await session.Catalog.ListAsync(session.Client, location, CancellationToken.None);

        rolledBack.Value.IsSaved.Should().BeTrue();
        var serverContent = await session.ReadTextAsync(path);
        serverContent.Should().Be(Original);
        backups.Value.Should().HaveCount(2);
        var newestBackup = await session.ReadTextAsync(backups.Value[0].Path);
        newestBackup.Should().Be(Edited);
    }

    [TestMethod]
    public async Task Save_PreservesPermissionBits()
    {
        await using var session = await EditorSession.OpenAsync();
        var directory = await session.CreateWorkDirectoryAsync();
        var path = await session.CreateFileAsync(directory, "deploy.sh", "#!/bin/sh\necho old\n");
        await session.ExecuteAsync($"chmod 750 '{path}'");
        var version = await session.OpenVersionAsync(path);

        var result = await session.SaveAsync(path, "#!/bin/sh\necho new\n", version);
        var mode = await session.ExecuteAsync($"stat -c %a '{path}'");
        var owner = await session.ExecuteAsync($"stat -c %U '{path}'");

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        mode.Should().Be("750");
        owner.Should().Be(SshServerContainer.UserName);
    }

    [TestMethod]
    public async Task Open_BinaryFile_IsRejected()
    {
        await using var session = await EditorSession.OpenAsync();
        var directory = await session.CreateWorkDirectoryAsync();
        var path = $"{directory}/blob.bin";
        await session.ExecuteAsync($"head -c 64 /dev/urandom > '{path}' && printf '\\000' >> '{path}' && chown {SshServerContainer.UserName}: '{path}'");

        var opened = await session.Documents.ReadAsync(session.Client, path, CancellationToken.None);

        opened.Error!.Code.Should().Be(EditorErrorCode.Binary);
    }
}
