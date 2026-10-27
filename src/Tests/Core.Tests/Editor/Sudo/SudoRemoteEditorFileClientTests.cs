using System.Text;
using DTech.Core.Connections;
using DTech.Core.Credentials;
using DTech.Core.Editor;
using DTech.Core.Editor.Sudo;
using DTech.Core.Files;
using FluentAssertions;

namespace DTech.Core.Tests.Editor.Sudo;

[TestClass]
public sealed class SudoRemoteEditorFileClientTests
{
    private const string Password = "s3cr3t-p@ss";
    private const string TargetPath = "/etc/nginx/site.conf";
    private const string StagedPath = "/tmp/tmp.Staged1";

    private ScriptedShellRunner _runner = null!;
    private SudoRemoteEditorFileClient _client = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Initialize()
    {
        _runner = new ScriptedShellRunner();
        var commands = new SudoCommandBuilder();
        var limits = new EditorLimits(1024, 3);
        using var secret = Secret.FromString(Password);
        var input = SudoPasswordInput.From(secret);
        var shell = new SudoShell(_runner, commands, input, limits);
        _client = new SudoRemoteEditorFileClient(shell, commands);
    }

    [TestMethod]
    public async Task GetAttributes_RunsStatWithSudoAndSendsPasswordOnlyOnStandardInput()
    {
        _runner.ReplySuccess(static text => text.Contains("stat -c", StringComparison.Ordinal), "81a4 12 1700000000 0 0\n");

        var attributes = await _client.GetAttributesAsync(TargetPath, TestContext.CancellationToken);

        attributes.IsSuccess.Should().BeTrue(attributes.Error?.Message);
        attributes.Value.Mode.Should().Be(0b110_100_100);
        var command = _runner.Commands.Should().ContainSingle().Subject;
        command.Text.Should().StartWith("LC_ALL=C sudo -S -p '' stat -c");
        command.Text.Should().NotContain(Password);
        command.InputText.Should().Be($"{Password}\n");
    }

    [TestMethod]
    public async Task GetAttributes_MissingFile_IsRemotePathNotFound()
    {
        _runner.ReplyFailure(static text => text.Contains("stat -c", StringComparison.Ordinal), "stat: cannot statx '/etc/nginx/site.conf': No such file or directory\n");

        var attributes = await _client.GetAttributesAsync(TargetPath, TestContext.CancellationToken);

        attributes.Error!.Kind.Should().Be(SshErrorKind.RemotePathNotFound);
    }

    [TestMethod]
    public async Task Read_LimitsOutputToOneByteOverTheLimit()
    {
        _runner.ReplySuccess(static text => text.Contains("head -c", StringComparison.Ordinal), "listen 80;\n");
        var request = new EditorReadRequest(TargetPath, 1024);

        var read = await _client.ReadAsync(request, TestContext.CancellationToken);

        Encoding.UTF8.GetString(read.Value).Should().Be("listen 80;\n");
        _runner.Commands[0].Text.Should().Contain("head -c 1025 -- '/etc/nginx/site.conf'");
    }

    [TestMethod]
    public async Task CreateFile_StagesContentAsUserThenInstallsWithOwnerAndModeAndRemovesStagedFile()
    {
        _runner.ReplySuccess(static text => text.Contains("mktemp", StringComparison.Ordinal), StagedPath);
        _runner.ReplySuccess(static text => text.Contains("stat -c", StringComparison.Ordinal), "81a0 6 1700000000 33 4\n");
        var content = Encoding.UTF8.GetBytes("new=1\n");
        var creation = new RemoteFileCreation(TargetPath, content, 0b110_100_000)
        {
            Owner = new RemoteFileOwner(33, 4),
        };

        var created = await _client.CreateFileAsync(creation, TestContext.CancellationToken);

        created.IsSuccess.Should().BeTrue(created.Error?.Message);
        created.Value.UserId.Should().Be(33);
        _runner.Commands.Should().HaveCount(3);
        var stage = _runner.Commands[0];
        stage.Text.Should().NotContain("sudo");
        stage.InputText.Should().Be("new=1\n");
        var install = _runner.Commands[1];
        install.Text.Should().StartWith("LC_ALL=C sudo -S -p '' sh -c ");
        install.Text.Should().Contain($"termforge-install '{StagedPath}' '{TargetPath}' 640 33 4; status=$?; rm -f -- '{StagedPath}'; exit $status");
        install.InputText.Should().Be($"{Password}\n");
        _runner.Commands.Should().OnlyContain(command => !command.Text.Contains(Password));
    }

    [TestMethod]
    public async Task CreateFile_WithoutOwner_InstallsAsRoot()
    {
        _runner.ReplySuccess(static text => text.Contains("mktemp", StringComparison.Ordinal), StagedPath);
        _runner.ReplySuccess(static text => text.Contains("stat -c", StringComparison.Ordinal), "8180 6 1700000000 0 0\n");
        var content = Encoding.UTF8.GetBytes("x");
        var creation = new RemoteFileCreation(TargetPath, content, 0b110_000_000);

        await _client.CreateFileAsync(creation, TestContext.CancellationToken);

        _runner.Commands[1].Text.Should().Contain(" 600 0 0;");
    }

    [TestMethod]
    public async Task CreateFile_InstallFailure_IsReportedAndNoAttributesAreRead()
    {
        _runner.ReplySuccess(static text => text.Contains("mktemp", StringComparison.Ordinal), StagedPath);
        _runner.ReplyFailure(static text => text.Contains("termforge-install", StringComparison.Ordinal), "install: cannot create regular file: Read-only file system\n");
        var content = Encoding.UTF8.GetBytes("x");
        var creation = new RemoteFileCreation(TargetPath, content, 0b110_000_000);

        var created = await _client.CreateFileAsync(creation, TestContext.CancellationToken);

        created.Error!.Kind.Should().Be(SshErrorKind.PermissionDenied);
        created.Error.Message.Should().Contain("Read-only file system");
        _runner.Commands.Should().HaveCount(2);
    }

    [TestMethod]
    public async Task CreateFile_StagingFailure_StopsBeforeSudo()
    {
        _runner.ReplyFailure(static text => text.Contains("mktemp", StringComparison.Ordinal), "mktemp: No space left on device\n");
        var content = Encoding.UTF8.GetBytes("x");
        var creation = new RemoteFileCreation(TargetPath, content, 0b110_000_000);

        var created = await _client.CreateFileAsync(creation, TestContext.CancellationToken);

        created.IsSuccess.Should().BeFalse();
        _runner.Commands.Should().ContainSingle();
    }

    [TestMethod]
    public async Task ReplaceAtomically_MovesWithSudo()
    {
        var request = new RemoteRenameRequest("/etc/nginx/.site.conf.termforge-1.tmp", TargetPath);

        var replaced = await _client.ReplaceAtomicallyAsync(request, TestContext.CancellationToken);

        replaced.Value.Should().Be(AtomicReplaceOutcome.Replaced);
        _runner.Commands[0].Text.Should().Contain("termforge-move '/etc/nginx/.site.conf.termforge-1.tmp' '/etc/nginx/site.conf'");
    }

    [TestMethod]
    public async Task ListNames_SplitsNulSeparatedOutput()
    {
        _runner.ReplySuccess(static text => text.Contains("termforge-list", StringComparison.Ordinal), "site.conf\0.site.conf.termforge-20260101T000000000Z.bak\0with\nnewline\0");

        var listed = await _client.ListNamesAsync("/etc/nginx", TestContext.CancellationToken);

        listed.Value.Should().Equal("site.conf", ".site.conf.termforge-20260101T000000000Z.bak", "with\nnewline");
    }

    [TestMethod]
    public async Task Dispose_ReleasesRunner()
    {
        await _client.DisposeAsync();

        _runner.Disposed.Should().BeTrue();
    }
}
