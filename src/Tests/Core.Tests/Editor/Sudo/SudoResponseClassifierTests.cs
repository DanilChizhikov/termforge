using DTech.Core.Editor.Sudo;
using FluentAssertions;

namespace DTech.Core.Tests.Editor.Sudo;

[TestClass]
public sealed class SudoResponseClassifierTests
{
    private readonly SudoResponseClassifier _classifier = new();

    [TestMethod]
    [DataRow(0, "", SudoResponse.Granted)]
    [DataRow(1, "sudo: a password is required\n", SudoResponse.PasswordRequired)]
    [DataRow(1, "Sorry, try again.\nsudo: 1 incorrect password attempt\n", SudoResponse.PasswordIncorrect)]
    [DataRow(1, "sudo: no password was provided\n", SudoResponse.PasswordRequired)]
    [DataRow(1, "tester is not in the sudoers file.\n", SudoResponse.NotPermitted)]
    [DataRow(1, "Sorry, user tester may not run sudo on host.\n", SudoResponse.NotPermitted)]
    [DataRow(127, "sh: sudo: not found\n", SudoResponse.NotInstalled)]
    [DataRow(127, "bash: sudo: command not found\n", SudoResponse.NotInstalled)]
    [DataRow(1, "sudo: sorry, you must have a tty to run sudo\n", SudoResponse.TerminalRequired)]
    [DataRow(1, "sudo: unable to resolve host\n", SudoResponse.Failed)]
    public void Classify_MapsSudoMessages(int exitCode, string standardError, SudoResponse expected)
    {
        var result = new RemoteShellCommandResult(exitCode, [], standardError);

        var response = _classifier.Classify(result);

        response.Should().Be(expected);
    }
}
