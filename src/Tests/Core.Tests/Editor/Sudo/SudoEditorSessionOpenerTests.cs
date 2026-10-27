using DTech.Core.Credentials;
using DTech.Core.Editor;
using DTech.Core.Editor.Sudo;
using FluentAssertions;

namespace DTech.Core.Tests.Editor.Sudo;

[TestClass]
public sealed class SudoEditorSessionOpenerTests
{
    private const string Password = "correct horse";

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
    public async Task Open_WithPassword_AuthenticatesOverStandardInputOnly()
    {
        _graph.AnswerAuthentication(0, string.Empty);
        var credentials = new SudoCredentials(Password, false);

        var opened = await _graph.Opener.OpenAsync(_connectionId, credentials, TestContext.CancellationToken);

        opened.IsSuccess.Should().BeTrue(opened.Error?.Message);
        opened.Value.ServerName.Should().Be(SudoEditorTestGraph.ServerName);
        var authentication = _graph.Runner.Commands.Should().ContainSingle().Subject;
        authentication.Text.Should().Be("LC_ALL=C sudo -S -p '' true");
        authentication.InputText.Should().Be($"{Password}\n");
        _graph.Credentials.Secrets.Should().BeEmpty();
        await opened.Value.DisposeAsync();
    }

    [TestMethod]
    public async Task Open_WrongPassword_FailsWithoutLeakingPasswordAndClosesConnection()
    {
        _graph.AnswerAuthentication(1, "Sorry, try again.\nsudo: 1 incorrect password attempt\n");
        var credentials = new SudoCredentials(Password, true);

        var opened = await _graph.Opener.OpenAsync(_connectionId, credentials, TestContext.CancellationToken);

        opened.Error!.Code.Should().Be(EditorErrorCode.SudoPasswordIncorrect);
        opened.Error.Message.Should().NotContain(Password);
        _graph.Runner.Disposed.Should().BeTrue();
        _graph.Credentials.Secrets.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Open_WithoutPasswordAndNothingSaved_UsesNonInteractiveSudo()
    {
        _graph.AnswerAuthentication(1, "sudo: a password is required\n");

        var opened = await _graph.Opener.OpenAsync(_connectionId, SudoCredentials.None, TestContext.CancellationToken);

        opened.Error!.Code.Should().Be(EditorErrorCode.SudoPasswordRequired);
        var authentication = _graph.Runner.Commands.Should().ContainSingle().Subject;
        authentication.Text.Should().Be("LC_ALL=C sudo -n true");
        authentication.StandardInput.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Open_UserWithoutSudo_IsSudoUnavailable()
    {
        _graph.AnswerAuthentication(1, "deploy is not in the sudoers file.\n");
        var credentials = new SudoCredentials(Password, false);

        var opened = await _graph.Opener.OpenAsync(_connectionId, credentials, TestContext.CancellationToken);

        opened.Error!.Code.Should().Be(EditorErrorCode.SudoUnavailable);
    }

    [TestMethod]
    public async Task Open_RememberPassword_SavesItOnlyAfterSuccessfulAuthentication()
    {
        _graph.AnswerAuthentication(0, string.Empty);
        var credentials = new SudoCredentials(Password, true);

        var opened = await _graph.Opener.OpenAsync(_connectionId, credentials, TestContext.CancellationToken);

        opened.IsSuccess.Should().BeTrue(opened.Error?.Message);
        var key = SudoPasswordStore.KeyFor(_connectionId);
        _graph.Credentials.Secrets.Should().ContainKey(key.Value).WhoseValue.Should().Be(Password);
        await opened.Value.DisposeAsync();
    }

    [TestMethod]
    public async Task Open_WithoutPassword_UsesSavedPassword()
    {
        _graph.AnswerAuthentication(0, string.Empty);
        using var saved = Secret.FromString(Password);
        await _graph.Store.SaveAsync(_connectionId, saved, TestContext.CancellationToken);

        var opened = await _graph.Opener.OpenAsync(_connectionId, SudoCredentials.None, TestContext.CancellationToken);

        opened.IsSuccess.Should().BeTrue(opened.Error?.Message);
        _graph.Runner.Commands[0].InputText.Should().Be($"{Password}\n");
        await opened.Value.DisposeAsync();
    }

    [TestMethod]
    public async Task Open_PasswordWithLineBreak_IsRejectedBeforeConnecting()
    {
        var credentials = new SudoCredentials("first\nsecond", false);

        var opened = await _graph.Opener.OpenAsync(_connectionId, credentials, TestContext.CancellationToken);

        opened.Error!.Code.Should().Be(EditorErrorCode.Invalid);
        _graph.Runner.Commands.Should().BeEmpty();
    }

    [TestMethod]
    public void Credentials_ToString_IsRedacted()
    {
        var credentials = new SudoCredentials(Password, true);

        var text = credentials.ToString();

        text.Should().NotContain(Password);
    }
}
