using DTech.Core.Editor;
using DTech.Core.Editor.Sudo;
using FluentAssertions;

namespace DTech.Core.Tests.Editor.Sudo;

[TestClass]
public sealed class SudoCommandBuilderTests
{
    private const string HostilePath = "/etc/it's $(id).conf";
    private const string QuotedHostilePath = "'/etc/it'\\''s $(id).conf'";

    private readonly SudoCommandBuilder _commands = new();

    [TestMethod]
    public void Probe_IsNonInteractiveSudoTrue()
    {
        var probe = _commands.Probe();

        probe.Should().Be("LC_ALL=C sudo -n true");
    }

    [TestMethod]
    public void Elevate_WithPassword_ReadsPasswordFromStandardInputWithEmptyPrompt()
    {
        var elevated = _commands.Elevate(true, "true");

        elevated.Should().Be("LC_ALL=C sudo -S -p '' true");
    }

    [TestMethod]
    public void Elevate_WithoutPassword_NeverPrompts()
    {
        var elevated = _commands.Elevate(false, "true");

        elevated.Should().Be("LC_ALL=C sudo -n true");
    }

    [TestMethod]
    public void Stat_QuotesFormatAndPath()
    {
        var stat = _commands.Stat(HostilePath);

        stat.Should().Be($"stat -c '%f %s %Y %u %g' -- {QuotedHostilePath}");
    }

    [TestMethod]
    public void ReadHead_LimitsBytesAndQuotesPath()
    {
        var read = _commands.ReadHead(HostilePath, 1025);

        read.Should().Be($"head -c 1025 -- {QuotedHostilePath}");
    }

    [TestMethod]
    public void Install_PassesValuesAsQuotedPositionalArguments()
    {
        var owner = new RemoteFileOwner(33, 4);
        var install = new StagedInstall("/tmp/tmp.AbC123", HostilePath, 0b100_111_101_000, owner);

        var command = _commands.Install(install);

        command.Should().StartWith("sh -c '");
        command.Should().EndWith($" termforge-install '/tmp/tmp.AbC123' {QuotedHostilePath} 4750 33 4");
        command.Should().Contain("install -o \"$4\" -g \"$5\" -m \"$3\" -- \"$1\" \"$2\"");
    }

    [TestMethod]
    public void Install_ScriptDoesNotContainAnyValue()
    {
        var owner = new RemoteFileOwner(0, 0);
        var install = new StagedInstall("/tmp/tmp.x", "/etc/target.conf", 0b110_100_100, owner);

        var command = _commands.Install(install);

        var scriptEnd = command.IndexOf(" termforge-install ", StringComparison.Ordinal);
        var script = command[..scriptEnd];
        script.Should().NotContain("/tmp/tmp.x").And.NotContain("/etc/target.conf");
    }

    [TestMethod]
    public void Move_RefusesDirectoryTargetAndQuotesPaths()
    {
        var move = _commands.Move("/etc/.a.termforge-1.tmp", HostilePath);

        move.Should().Contain("mv -f -- \"$1\" \"$2\"");
        move.Should().EndWith($" termforge-move '/etc/.a.termforge-1.tmp' {QuotedHostilePath}");
    }

    [TestMethod]
    public void WithCleanup_RemovesStagedFileAndKeepsExitStatus()
    {
        var command = _commands.WithCleanup("LC_ALL=C sudo -n true", "/tmp/tmp.x");

        command.Should().Be("LC_ALL=C sudo -n true; status=$?; rm -f -- '/tmp/tmp.x'; exit $status");
    }

    [TestMethod]
    public void ChangeOwner_UsesNumericIds()
    {
        var ownership = new RemoteOwnership(HostilePath, 101, 102);

        var command = _commands.ChangeOwner(ownership);

        command.Should().Be($"chown -- 101:102 {QuotedHostilePath}");
    }

    [TestMethod]
    public void Delete_AndListNames_QuotePaths()
    {
        var delete = _commands.Delete(HostilePath);
        var list = _commands.ListNames("/etc/my dir");

        delete.Should().Be($"rm -f -- {QuotedHostilePath}");
        list.Should().EndWith(" termforge-list '/etc/my dir'");
    }

    [TestMethod]
    public void StageUpload_CreatesPrivateTemporaryFileAndPrintsItsPath()
    {
        var stage = _commands.StageUpload();

        stage.Should().Contain("umask 077").And.Contain("mktemp").And.Contain("cat > \"$staged\"");
        stage.Should().NotContain("sudo");
    }
}
