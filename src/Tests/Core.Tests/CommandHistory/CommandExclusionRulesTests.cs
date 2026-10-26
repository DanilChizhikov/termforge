using DTech.Core.CommandHistory;
using FluentAssertions;

namespace DTech.Core.Tests.CommandHistory;

[TestClass]
public sealed class CommandExclusionRulesTests
{
    private static readonly CommandExclusionRules Defaults = CommandExclusionRules.Create(DefaultCommandExclusionPatterns.Values);

    [TestMethod]
    [DataRow("ls -la")]
    [DataRow("mkdir -p /var/www")]
    [DataRow("git commit -m \"fix key handling\"")]
    [DataRow("docker ps --format '{{.Names}}'")]
    [DataRow("sudo systemctl restart nginx")]
    public void FindReason_OrdinaryCommands_AreKept(string command)
    {
        Defaults.FindReason(command).Should().BeNull();
    }

    [TestMethod]
    [DataRow(" ls")]
    [DataRow("\tls")]
    public void FindReason_LeadingWhitespace_IsExcluded(string command)
    {
        Defaults.FindReason(command).Should().Be(CommandExclusionReason.LeadingSpace);
    }

    [TestMethod]
    [DataRow("export AWS_SECRET_ACCESS_KEY=abc123")]
    [DataRow("export GITHUB_TOKEN=ghp_x")]
    [DataRow("export DB_PASSWORD='p'")]
    [DataRow("API_KEY=abc ./run.sh")]
    [DataRow("mysql -u root -pHunter2")]
    [DataRow("curl -H 'Authorization: Bearer abc' https://x.test")]
    [DataRow("curl https://user:pass@example.test/file")]
    [DataRow("sshpass -p x ssh host --password hunter2")]
    [DataRow("passwd")]
    [DataRow("echo $TOKEN")]
    public void FindReason_LikelySecrets_AreExcludedByDefaultPatterns(string command)
    {
        Defaults.FindReason(command).Should().Be(CommandExclusionReason.MatchesPattern);
    }

    [TestMethod]
    public void FindReason_CustomPatterns_ReplaceTheDefaults()
    {
        var rules = CommandExclusionRules.Create([@"^deploy\b"]);

        rules.FindReason("deploy prod").Should().Be(CommandExclusionReason.MatchesPattern);
        rules.FindReason("export TOKEN=abc").Should().BeNull();
    }

    [TestMethod]
    public void FindReason_ControlCharactersAndOversizedAndEmpty_AreExcluded()
    {
        Defaults.FindReason("echo \u001b[31m").Should().Be(CommandExclusionReason.ControlCharacters);
        Defaults.FindReason(new string('a', CommandHistoryLimits.MaxCommandLength + 1)).Should().Be(CommandExclusionReason.TooLong);
        Defaults.FindReason("   ").Should().Be(CommandExclusionReason.Empty);
    }

    [TestMethod]
    public void Create_InvalidPattern_IsIgnoredInsteadOfBlockingHistory()
    {
        var rules = CommandExclusionRules.Create(["(unclosed", "secret"]);

        rules.FindReason("echo secret").Should().Be(CommandExclusionReason.MatchesPattern);
        rules.FindReason("ls").Should().BeNull();
    }

    [TestMethod]
    public void Validate_RejectsInvalidAndOversizedPatterns()
    {
        CommandExclusionRules.Validate("(unclosed").Should().Contain("not a valid regular expression");
        CommandExclusionRules.Validate("").Should().Contain("1 to");
        CommandExclusionRules.Validate(new string('a', CommandHistoryLimits.MaxExclusionPatternLength + 1)).Should().Contain("1 to");
        CommandExclusionRules.Validate("^ok$").Should().BeNull();
    }
}
