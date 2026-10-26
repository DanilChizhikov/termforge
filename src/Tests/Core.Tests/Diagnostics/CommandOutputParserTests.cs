using DTech.Core.Diagnostics;
using DTech.Core.Diagnostics.Parsing;
using DTech.Core.Tests.Diagnostics.Support;
using FluentAssertions;

namespace DTech.Core.Tests.Diagnostics;

[TestClass]
public sealed class CommandOutputParserTests
{
    [TestMethod]
    public void Docker_WithVersionAndZeroExit_IsAccessible()
    {
        var result = new RemoteCommandResult(0, "27.3.1\n", string.Empty);

        var info = DockerAccessParser.Parse(result);

        info.Access.Should().Be(DockerAccess.Accessible);
        info.Version.Should().Be("27.3.1");
    }

    [TestMethod]
    public void Docker_PermissionDenied_IsDetected()
    {
        var result = new RemoteCommandResult(1, RecordedOutputs.DockerPermissionDenied, string.Empty);

        var info = DockerAccessParser.Parse(result);

        info.Access.Should().Be(DockerAccess.PermissionDenied);
    }

    [TestMethod]
    public void Docker_DaemonDown_IsDetected()
    {
        var result = new RemoteCommandResult(1, RecordedOutputs.DockerDaemonDown, string.Empty);

        var info = DockerAccessParser.Parse(result);

        info.Access.Should().Be(DockerAccess.DaemonNotRunning);
    }

    [TestMethod]
    [DataRow(0, "<html>not a version</html>")]
    [DataRow(0, "")]
    [DataRow(2, "something odd")]
    public void Docker_UnrecognizedOutput_IsUnknown(int exitCode, string output)
    {
        var result = new RemoteCommandResult(exitCode, output, string.Empty);

        var info = DockerAccessParser.Parse(result);

        info.Access.Should().Be(DockerAccess.Unknown);
    }

    [TestMethod]
    public void Docker_OversizedGarbage_IsUnknown()
    {
        var result = new RemoteCommandResult(0, new string('x', 200_000), string.Empty) { OutputTruncated = true };

        var info = DockerAccessParser.Parse(result);

        info.Access.Should().Be(DockerAccess.Unknown);
    }

    [TestMethod]
    public void Sudo_ZeroExit_IsPasswordless()
    {
        var result = new RemoteCommandResult(0, string.Empty, string.Empty);

        SudoOutputParser.Parse(result).Should().Be(SudoAccess.Passwordless);
    }

    [TestMethod]
    [DataRow(RecordedOutputs.SudoPasswordRequired, SudoAccess.PasswordRequired)]
    [DataRow(RecordedOutputs.SudoTerminalRequired, SudoAccess.PasswordRequired)]
    [DataRow(RecordedOutputs.SudoNotInSudoers, SudoAccess.NotPermitted)]
    [DataRow("something unexpected", SudoAccess.Unknown)]
    [DataRow("", SudoAccess.Unknown)]
    public void Sudo_FailureOutput_IsClassified(string output, SudoAccess expected)
    {
        var result = new RemoteCommandResult(1, output, string.Empty);

        SudoOutputParser.Parse(result).Should().Be(expected);
    }

    [TestMethod]
    public void Utilities_BusyBox_AllPresent()
    {
        var answers = UtilityListParser.Parse(RecordedOutputs.BusyBoxUtilities);

        answers.Should().BeEquivalentTo(new Dictionary<string, bool> { ["stat"] = true, ["df"] = true, ["free"] = true, ["uptime"] = true });
    }

    [TestMethod]
    public void Utilities_MissingTools_AreReportedAsAbsent()
    {
        var answers = UtilityListParser.Parse(RecordedOutputs.MinimalContainerUtilities);

        answers["free"].Should().BeFalse();
        answers["uptime"].Should().BeFalse();
        answers["stat"].Should().BeTrue();
    }

    [TestMethod]
    public void Utilities_GarbageAndUnexpectedKeys_AreIgnored()
    {
        var output = "stat=maybe\nrm=yes\n=yes\nnoseparator\n\u0000\ndf=yes\n";

        var answers = UtilityListParser.Parse(output);

        answers.Should().BeEquivalentTo(new Dictionary<string, bool> { ["df"] = true });
    }

    [TestMethod]
    public void ShellScript_Wrap_EscapesSingleQuotes()
    {
        var wrapped = ShellScript.Wrap("echo 'a'");

        wrapped.Should().Be("sh -c 'echo '\\''a'\\'''");
    }

    [TestMethod]
    public void RemoteText_Sanitize_ReplacesControlCharactersAndLimitsLength()
    {
        var sanitized = RemoteText.Sanitize("a\u001b[0mb\nc", 4);

        sanitized.Should().Be("a [0");
    }
}
