using System.Text;
using DTech.Core.Editor;
using DTech.Core.Editor.Sudo;
using FluentAssertions;

namespace DTech.Ssh.IntegrationTests.Editor;

[TestClass]
[DoNotParallelize]
public sealed class SudoFishShellTests
{
    private const string UserName = "fishsudoer";
    private const string Password = "fish-sudoer-password";
    private const string Original = "listen 80;\n";
    private const string Edited = "listen 8080;\n";

    [TestMethod]
    public async Task Probe_FishLoginShell_ReportsPasswordRequired()
    {
        await using var session = await SudoEditorSession.OpenAsync();
        var runner = await session.ConnectAsync(UserName, Password);

        var result = await session.Probe.ProbeAsync(runner, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.Availability.Should().Be(SudoAvailability.PasswordRequired);
    }

    [TestMethod]
    public async Task Save_FishLoginShell_PreservesFileAndBackupAttributesAndCleansStaging()
    {
        await using var session = await SudoEditorSession.OpenAsync();
        var directory = await session.CreateProtectedDirectoryAsync();
        var file = new ProtectedFile(directory, "it's a $(touch pwned) `id` file.conf", Original)
        {
            Owner = "2:4",
            Mode = "640",
        };
        var path = await session.CreateProtectedFileAsync(file);
        var elevated = await session.ElevateAsync(UserName, Password, Password);
        elevated.IsSuccess.Should().BeTrue(elevated.Error?.Message);
        var client = elevated.Value;
        var version = await session.OpenVersionAsync(client, path);

        var result = await session.SaveAsync(client, path, Edited, version);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.IsSaved.Should().BeTrue();
        var content = await session.ReadAsRootAsync(path);
        content.Should().Be(Edited);
        var attributes = await session.DescribeAsync(path);
        attributes.Should().Be("2 4 640");
        var backup = result.Value.Saved!.Backup!;
        var backupContent = await session.ReadAsRootAsync(backup.Path);
        backupContent.Should().Be(Original);
        var backupAttributes = await session.DescribeAsync(backup.Path);
        backupAttributes.Should().Be("2 4 640");
        var staged = await session.ExecuteAsync($"find /tmp -maxdepth 1 -user {UserName}");
        staged.Should().BeEmpty();
        var injected = await session.ExecuteAsync($"find '{directory}' /tmp /home -maxdepth 2 -name pwned");
        injected.Should().BeEmpty();
    }

    [TestMethod]
    public async Task CreateFile_FishLoginShell_WhenTargetExists_KeepsOriginalAndCleansStaging()
    {
        await using var session = await SudoEditorSession.OpenAsync();
        var directory = await session.CreateProtectedDirectoryAsync();
        var file = new ProtectedFile(directory, "existing.conf", Original);
        var path = await session.CreateProtectedFileAsync(file);
        var elevated = await session.ElevateAsync(UserName, Password, Password);
        elevated.IsSuccess.Should().BeTrue(elevated.Error?.Message);
        var content = Encoding.UTF8.GetBytes(Edited);
        var creation = new RemoteFileCreation(path, content, 0b110_000_000);

        var result = await elevated.Value.CreateFileAsync(creation, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        var original = await session.ReadAsRootAsync(path);
        original.Should().Be(Original);
        var staged = await session.ExecuteAsync($"find /tmp -maxdepth 1 -user {UserName}");
        staged.Should().BeEmpty();
    }
}
