using DTech.Core.Credentials;
using DTech.Core.Editor.Sudo;
using FluentAssertions;

namespace DTech.Core.Tests.Editor.Sudo;

[TestClass]
public sealed class SudoStatusReaderTests
{
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
    [DataRow(0, "", SudoAvailability.Passwordless)]
    [DataRow(1, "sudo: a password is required\n", SudoAvailability.PasswordRequired)]
    [DataRow(127, "sh: sudo: not found\n", SudoAvailability.Unavailable)]
    [DataRow(1, "Sorry, user deploy may not run sudo on web-01.\n", SudoAvailability.Unavailable)]
    public async Task Read_ProbesWithNonInteractiveSudo(int exitCode, string standardError, SudoAvailability expected)
    {
        _graph.AnswerAuthentication(exitCode, standardError);

        var status = await _graph.StatusReader.ReadAsync(_connectionId, TestContext.CancellationToken);

        status.Value.Availability.Should().Be(expected);
        status.Value.HasSavedPassword.Should().BeFalse();
        var probe = _graph.Runner.Commands.Should().ContainSingle().Subject;
        probe.Text.Should().Be("LC_ALL=C sudo -n true");
        probe.StandardInput.Should().BeEmpty();
        _graph.Runner.Disposed.Should().BeTrue();
    }

    [TestMethod]
    public async Task Read_ReportsSavedPassword()
    {
        _graph.AnswerAuthentication(1, "sudo: a password is required\n");
        using var saved = Secret.FromString("pw");
        await _graph.Store.SaveAsync(_connectionId, saved, TestContext.CancellationToken);

        var status = await _graph.StatusReader.ReadAsync(_connectionId, TestContext.CancellationToken);

        status.Value.HasSavedPassword.Should().BeTrue();
    }

    [TestMethod]
    public async Task ForgetPassword_RemovesSavedPassword()
    {
        using var saved = Secret.FromString("pw");
        await _graph.Store.SaveAsync(_connectionId, saved, TestContext.CancellationToken);

        var failure = await _graph.Store.DeleteAsync(_connectionId, TestContext.CancellationToken);
        var again = await _graph.Store.DeleteAsync(_connectionId, TestContext.CancellationToken);

        failure.Should().BeNull();
        again.Should().BeNull();
        _graph.Credentials.Secrets.Should().BeEmpty();
    }
}
