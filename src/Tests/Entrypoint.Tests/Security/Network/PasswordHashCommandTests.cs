using DTech.Entrypoint.Security.Network;
using FluentAssertions;

namespace DTech.Entrypoint.Tests.Security.Network;

[TestClass]
public sealed class PasswordHashCommandTests
{
    [TestMethod]
    public void Command_prints_a_hash_that_verifies_the_password()
    {
        const string password = "a long enough password";
        using var input = new StringReader(password);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = PasswordHashCommand.Run(input, output, error);

        exitCode.Should().Be(0);
        var printedHash = output.ToString().Trim();
        printedHash.Should().NotContain(password);
        PasswordHash.TryParse(printedHash, out var passwordHash).Should().BeTrue();
        passwordHash!.Iterations.Should().Be(PasswordHash.DefaultIterations);
        passwordHash.Verify(password).Should().BeTrue();
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("short")]
    public void Command_rejects_short_password(string password)
    {
        using var input = new StringReader(password);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = PasswordHashCommand.Run(input, output, error);

        exitCode.Should().Be(1);
        output.ToString().Should().BeEmpty();
        error.ToString().Should().Contain("at least 12 characters");
    }

    [TestMethod]
    public void Command_is_not_run_without_its_argument()
    {
        string[] args = ["--Lifecycle:OpenBrowser=false"];

        var hasRun = PasswordHashCommand.TryRun(args);

        hasRun.Should().BeFalse();
    }
}
