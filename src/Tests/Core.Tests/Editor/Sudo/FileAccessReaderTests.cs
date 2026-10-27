using DTech.Core.Editor;
using DTech.Core.Editor.Sudo;
using FluentAssertions;

namespace DTech.Core.Tests.Editor.Sudo;

[TestClass]
public sealed class FileAccessReaderTests
{
    private const string HostilePath = "/etc/it's $(id)/app `x`.conf";

    private SudoEditorTestGraph _graph = null!;
    private Guid _connectionId;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public async Task Initialize()
    {
        _graph = new SudoEditorTestGraph();
        _connectionId = await _graph.AddConnectionAsync(TestContext.CancellationToken);
    }

    [TestMethod]
    public void Probe_PassesUidPathAndDirectoryAsQuotedPositionalArguments()
    {
        var commands = new FileAccessCommands();
        var location = new RemoteFileLocation("/etc/it's $(id)", "app `x`.conf");

        var probe = commands.Probe(0, location);

        probe.Should().StartWith("sh -c '");
        probe.Should().EndWith(" termforge-access 0 '/etc/it'\\''s $(id)/app `x`.conf' '/etc/it'\\''s $(id)'");
        var scriptEnd = probe.IndexOf(" termforge-access ", StringComparison.Ordinal);
        probe[..scriptEnd].Should().NotContain("$(id)").And.NotContain("app `x`.conf");
        probe.Should().Contain("getent passwd").And.Contain("[ -w \"$2\" ] && [ -w \"$3\" ]");
        probe.Should().NotContain("sudo");
    }

    [TestMethod]
    [DataRow("0\nroot\n", false, "root")]
    [DataRow("1\ndeploy\n", true, "deploy")]
    [DataRow("0\n\n", false, null)]
    [DataRow("0\n", false, null)]
    [DataRow("0\nbad name\n", false, null)]
    public void Parse_ReadsWritableFlagAndOwnerName(string output, bool writable, string? ownerName)
    {
        var access = FileAccessOutputParser.Parse(output, 7);

        var expected = new RemoteFileAccess(7, ownerName, writable);
        access.Should().Be(expected);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("yes\nroot\n")]
    public void Parse_UnexpectedOutput_ReturnsNull(string output)
    {
        var access = FileAccessOutputParser.Parse(output, 0);

        access.Should().BeNull();
    }

    [TestMethod]
    public async Task Read_RunsUnprivilegedProbeAndReturnsOwnerAndWritability()
    {
        _graph.Runner.ReplySuccess(static text => text.Contains("termforge-access", StringComparison.Ordinal), "0\nroot\n");
        var request = new FileAccessRequest(_connectionId, HostilePath, 0);

        var access = await _graph.AccessReader.ReadAsync(request, TestContext.CancellationToken);

        var expected = new RemoteFileAccess(0, "root", false);
        access.Value.Should().Be(expected);
        var command = _graph.Runner.Commands.Should().ContainSingle().Subject;
        command.Text.Should().NotStartWith("LC_ALL=C sudo");
        command.Text.Should().EndWith(" termforge-access 0 '/etc/it'\\''s $(id)/app `x`.conf' '/etc/it'\\''s $(id)'");
        command.StandardInput.Should().BeEmpty();
        _graph.Runner.Disposed.Should().BeTrue();
    }

    [TestMethod]
    public async Task Read_UnknownOwner_KeepsUidWithoutName()
    {
        _graph.Runner.ReplySuccess(static text => text.Contains("termforge-access", StringComparison.Ordinal), "0\n\n");
        var request = new FileAccessRequest(_connectionId, "/srv/app.conf", 4242);

        var access = await _graph.AccessReader.ReadAsync(request, TestContext.CancellationToken);

        var expected = new RemoteFileAccess(4242, null, false);
        access.Value.Should().Be(expected);
    }

    [TestMethod]
    public async Task Read_RelativePath_IsInvalidAndNothingRuns()
    {
        var request = new FileAccessRequest(_connectionId, "etc/app.conf", 0);

        var access = await _graph.AccessReader.ReadAsync(request, TestContext.CancellationToken);

        access.Error!.Code.Should().Be(EditorErrorCode.Invalid);
        _graph.Runner.Commands.Should().BeEmpty();
    }
}
