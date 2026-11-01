using System.Text;
using DTech.Core.Editor;
using DTech.Core.Editor.Sudo;
using FluentAssertions;

namespace DTech.Core.Tests.Editor.Sudo;

[TestClass]
public sealed class SudoShellTests
{
    [TestMethod]
    public async Task StageUpload_WrapsPosixSyntaxAndPreservesStandardInput()
    {
        var runner = new ScriptedShellRunner();
        var commands = new SudoCommandBuilder();
        var limits = new EditorLimits(1024, 3);
        await using var shell = new SudoShell(runner, commands, null, limits);
        var content = Encoding.UTF8.GetBytes("data with 'quotes' and $variables\n");
        var script = commands.StageUpload();

        await shell.RunAsUserAsync(script, content, CancellationToken.None);

        var command = runner.Commands.Should().ContainSingle().Subject;
        command.Text.Should().StartWith("sh -c '");
        command.Text.Should().Contain("staged=$(mktemp)");
        command.StandardInput.Should().Equal(content);
    }

    [TestMethod]
    public async Task ElevatedCleanup_WrapsExitStatusAndCleanupInSamePosixShell()
    {
        var runner = new ScriptedShellRunner();
        var commands = new SudoCommandBuilder();
        var limits = new EditorLimits(1024, 3);
        await using var shell = new SudoShell(runner, commands, null, limits);

        await shell.RunElevatedWithCleanupAsync("false", "/tmp/staged file", CancellationToken.None);

        var command = runner.Commands.Should().ContainSingle().Subject;
        command.Text.Should().StartWith("sh -c 'LC_ALL=C sudo -n false; status=$?;");
        command.Text.Should().EndWith("; exit $status'");
        command.StandardInput.Should().BeEmpty();
    }
}
