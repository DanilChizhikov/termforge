using DTech.Core.Editor;
using DTech.Core.Editor.Sudo;
using DTech.Ssh.IntegrationTests.Harness;
using FluentAssertions;

namespace DTech.Ssh.IntegrationTests.Editor;

[TestClass]
[DoNotParallelize]
public sealed class SudoFileEditorTests
{
    private const string Original = "listen 80;\n";
    private const string Edited = "listen 8080;\n";

    [TestMethod]
    public async Task Probe_UserWithPasswordSudo_ReportsPasswordRequired()
    {
        await using var session = await SudoEditorSession.OpenAsync();
        var runner = await session.ConnectAsync(SshServerContainer.SudoUserName, SshServerContainer.SudoPassword);

        var probed = await session.Probe.ProbeAsync(runner, CancellationToken.None);

        probed.IsSuccess.Should().BeTrue(probed.Error?.Message);
        probed.Value.Availability.Should().Be(SudoAvailability.PasswordRequired);
    }

    [TestMethod]
    public async Task Probe_UserWithoutSudo_IsNotPasswordless()
    {
        await using var session = await SudoEditorSession.OpenAsync();
        var runner = await session.ConnectAsync(SshServerContainer.UserName, SshServerContainer.Password);

        var probed = await session.Probe.ProbeAsync(runner, CancellationToken.None);

        probed.IsSuccess.Should().BeTrue(probed.Error?.Message);
        probed.Value.Availability.Should().NotBe(SudoAvailability.Passwordless);
    }

    [TestMethod]
    public async Task Elevate_WithWrongPassword_FailsWithPasswordIncorrectAndKeepsFile()
    {
        await using var session = await SudoEditorSession.OpenAsync();
        var directory = await session.CreateProtectedDirectoryAsync();
        var file = new ProtectedFile(directory, "app.conf", Original);
        var path = await session.CreateProtectedFileAsync(file);

        var elevated = await session.ElevateAsync(SshServerContainer.SudoUserName, SshServerContainer.SudoPassword, "wrong-password");

        elevated.IsSuccess.Should().BeFalse();
        elevated.Error!.Code.Should().Be(EditorErrorCode.SudoPasswordIncorrect);
        elevated.Error.Message.Should().NotContain("wrong-password");
        var content = await session.ReadAsRootAsync(path);
        content.Should().Be(Original);
    }

    [TestMethod]
    public async Task Elevate_WithoutPassword_FailsWithPasswordRequired()
    {
        await using var session = await SudoEditorSession.OpenAsync();

        var elevated = await session.ElevateAsync(SshServerContainer.SudoUserName, SshServerContainer.SudoPassword, null);

        elevated.IsSuccess.Should().BeFalse();
        elevated.Error!.Code.Should().Be(EditorErrorCode.SudoPasswordRequired);
    }

    [TestMethod]
    public async Task Elevate_UserWithoutSudo_FailsWithSudoUnavailable()
    {
        await using var session = await SudoEditorSession.OpenAsync();

        var elevated = await session.ElevateAsync(SshServerContainer.UserName, SshServerContainer.Password, SshServerContainer.Password);

        elevated.IsSuccess.Should().BeFalse();
        elevated.Error!.Code.Should().Be(EditorErrorCode.SudoUnavailable);
    }

    [TestMethod]
    public async Task Open_RootOnlyFile_ReturnsContentAndMode()
    {
        await using var session = await SudoEditorSession.OpenAsync();
        var directory = await session.CreateProtectedDirectoryAsync();
        var file = new ProtectedFile(directory, "secret.conf", Original);
        var path = await session.CreateProtectedFileAsync(file);
        var client = await session.ElevateSudoerAsync();

        var opened = await session.Documents.ReadAsync(client, path, CancellationToken.None);

        opened.IsSuccess.Should().BeTrue(opened.Error?.Message);
        opened.Value.Content.Text.Should().Be(Original);
        opened.Value.Mode.Should().Be(0b110_000_000);
        opened.Value.Version.Size.Should().Be(Original.Length);
    }

    [TestMethod]
    public async Task Save_PreservesOwnerGroupAndModeAndKeepsBackup()
    {
        await using var session = await SudoEditorSession.OpenAsync();
        var directory = await session.CreateProtectedDirectoryAsync();
        var file = new ProtectedFile(directory, "site.conf", Original) { Owner = "2:4", Mode = "640" };
        var path = await session.CreateProtectedFileAsync(file);
        var client = await session.ElevateSudoerAsync();
        var version = await session.OpenVersionAsync(client, path);

        var result = await session.SaveAsync(client, path, Edited, version);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.Saved!.Strategy.Should().Be(SaveStrategy.AtomicRename);
        var content = await session.ReadAsRootAsync(path);
        content.Should().Be(Edited);
        var attributes = await session.DescribeAsync(path);
        attributes.Should().Be("2 4 640");
        var backup = result.Value.Saved.Backup!;
        var backupContent = await session.ReadAsRootAsync(backup.Path);
        backupContent.Should().Be(Original);
        var backupAttributes = await session.DescribeAsync(backup.Path);
        backupAttributes.Should().Be("2 4 640");
        var listing = await session.ExecuteAsync($"ls -A '{directory}'");
        var names = listing.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        names.Should().BeEquivalentTo("site.conf", backup.Name);
        var staged = await session.ExecuteAsync($"find /tmp -maxdepth 1 -user {SshServerContainer.SudoUserName}");
        staged.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Save_SetUserIdExecutable_KeepsSpecialBits()
    {
        await using var session = await SudoEditorSession.OpenAsync();
        var directory = await session.CreateProtectedDirectoryAsync();
        var file = new ProtectedFile(directory, "tool.sh", "#!/bin/sh\necho old\n") { Mode = "4750" };
        var path = await session.CreateProtectedFileAsync(file);
        var client = await session.ElevateSudoerAsync();
        var version = await session.OpenVersionAsync(client, path);

        var result = await session.SaveAsync(client, path, "#!/bin/sh\necho new\n", version);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        var attributes = await session.DescribeAsync(path);
        attributes.Should().Be("0 0 4750");
    }

    [TestMethod]
    public async Task Save_NameWithQuotesAndSpaces_IsQuotedSafely()
    {
        await using var session = await SudoEditorSession.OpenAsync();
        var directory = await session.CreateProtectedDirectoryAsync();
        var file = new ProtectedFile(directory, "it's a \"$(touch pwned)\" `id` file.conf", Original);
        var path = await session.CreateProtectedFileAsync(file);
        var client = await session.ElevateSudoerAsync();
        var version = await session.OpenVersionAsync(client, path);

        var result = await session.SaveAsync(client, path, Edited, version);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        var content = await session.ReadAsRootAsync(path);
        content.Should().Be(Edited);
        var injected = await session.ExecuteAsync($"find '{directory}' /tmp /root /home -maxdepth 2 -name pwned");
        injected.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Save_AfterExternalModification_ReturnsConflictAndKeepsServerContent()
    {
        await using var session = await SudoEditorSession.OpenAsync();
        var directory = await session.CreateProtectedDirectoryAsync();
        var file = new ProtectedFile(directory, "site.conf", Original);
        var path = await session.CreateProtectedFileAsync(file);
        var client = await session.ElevateSudoerAsync();
        var version = await session.OpenVersionAsync(client, path);
        await session.ExecuteAsync($"printf 'changed elsewhere\\n' > '{path}'");

        var result = await session.SaveAsync(client, path, Edited, version);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.IsSaved.Should().BeFalse();
        result.Value.Conflict!.CurrentContent!.Value.Text.Should().Be("changed elsewhere\n");
        var content = await session.ReadAsRootAsync(path);
        content.Should().Be("changed elsewhere\n");
    }

    [TestMethod]
    public async Task Save_WhenReplaceFails_KeepsOriginalAndRemovesTemporaryFile()
    {
        await using var session = await SudoEditorSession.OpenAsync();
        var directory = await session.CreateProtectedDirectoryAsync();
        var file = new ProtectedFile(directory, "site.conf", Original) { Mode = "644" };
        var path = await session.CreateProtectedFileAsync(file);
        var elevated = await session.ElevateSudoerAsync();
        var client = new FailingReplaceEditorFileClient(elevated);
        var version = await session.OpenVersionAsync(client, path);

        var result = await session.SaveAsync(client, path, Edited, version);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Should().Contain("was not changed");
        var content = await session.ReadAsRootAsync(path);
        content.Should().Be(Original);
        var attributes = await session.DescribeAsync(path);
        attributes.Should().Be("0 0 644");
        var temporaryFiles = await session.ExecuteAsync($"find '{directory}' -name '*.tmp'");
        temporaryFiles.Should().BeEmpty();
        var staged = await session.ExecuteAsync($"find /tmp -maxdepth 1 -user {SshServerContainer.SudoUserName}");
        staged.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Rollback_RestoresBackupWithOwnerAndMode()
    {
        await using var session = await SudoEditorSession.OpenAsync();
        var directory = await session.CreateProtectedDirectoryAsync();
        var file = new ProtectedFile(directory, "site.conf", Original) { Owner = "2:4", Mode = "640" };
        var path = await session.CreateProtectedFileAsync(file);
        var client = await session.ElevateSudoerAsync();
        var version = await session.OpenVersionAsync(client, path);
        var saved = await session.SaveAsync(client, path, Edited, version);
        var location = session.Locate(path);
        var request = new RollbackRequest(location, saved.Value.Saved!.Backup!.Name, saved.Value.Saved.Version);

        var rolledBack = await session.Rollback.RollbackAsync(client, request, CancellationToken.None);
        var backups = await session.Catalog.ListAsync(client, location, CancellationToken.None);

        rolledBack.IsSuccess.Should().BeTrue(rolledBack.Error?.Message);
        rolledBack.Value.IsSaved.Should().BeTrue();
        var content = await session.ReadAsRootAsync(path);
        content.Should().Be(Original);
        var attributes = await session.DescribeAsync(path);
        attributes.Should().Be("2 4 640");
        backups.Value.Should().HaveCount(2);
    }

    [TestMethod]
    public async Task Access_RootOwnedFile_IsNotWritableAndNamesRoot()
    {
        await using var session = await SudoEditorSession.OpenAsync();
        var directory = await session.CreateProtectedDirectoryAsync();
        var file = new ProtectedFile(directory, "it's $(id) `x`.conf", Original) { Mode = "644" };
        var path = await session.CreateProtectedFileAsync(file);

        var access = await session.ProbeAccessAsTesterAsync(0, path);

        var expected = new RemoteFileAccess(0, "root", false);
        access.Should().Be(expected);
    }

    [TestMethod]
    public async Task Access_OwnFileInOwnDirectory_IsWritable()
    {
        await using var session = await SudoEditorSession.OpenAsync();
        var directory = $"{SshServerContainer.HomeDirectory}/access-{Guid.NewGuid():N}";
        await session.ExecuteAsync($"mkdir '{directory}' && chown {SshServerContainer.UserName}: '{directory}'");
        var file = new ProtectedFile(directory, "app.conf", Original) { Owner = $"{SshServerContainer.UserName}:", Mode = "644" };
        var path = await session.CreateProtectedFileAsync(file);
        var userId = await session.ExecuteAsync($"id -u {SshServerContainer.UserName}");
        var ownerId = int.Parse(userId, System.Globalization.CultureInfo.InvariantCulture);

        var access = await session.ProbeAccessAsTesterAsync(ownerId, path);

        var expected = new RemoteFileAccess(ownerId, SshServerContainer.UserName, true);
        access.Should().Be(expected);
    }

    [TestMethod]
    public async Task Access_UidWithoutAccount_HasNoOwnerName()
    {
        await using var session = await SudoEditorSession.OpenAsync();
        var directory = await session.CreateProtectedDirectoryAsync();
        var file = new ProtectedFile(directory, "orphan.conf", Original) { Owner = "4242:4242", Mode = "644" };
        var path = await session.CreateProtectedFileAsync(file);

        var access = await session.ProbeAccessAsTesterAsync(4242, path);

        var expected = new RemoteFileAccess(4242, null, false);
        access.Should().Be(expected);
    }
}
