using DTech.Core.Operations;
using FluentAssertions;

namespace DTech.Core.Tests.Operations;

[TestClass]
public sealed class OperationSanitizerTests
{
    private readonly OperationSanitizer _sanitizer = new();

    [TestMethod]
    [DataRow("password")]
    [DataRow("sshPassphrase")]
    [DataRow("access_token")]
    [DataRow("ClientSecret")]
    [DataRow("privateKey")]
    [DataRow("apiKey")]
    public void Sensitive_keys_have_values_redacted(string key)
    {
        var details = new Dictionary<string, string> { [key] = "hunter2" };

        var sanitized = _sanitizer.SanitizeDetails(details);

        sanitized[key].Should().Be(OperationSanitizer.Redacted);
    }

    [TestMethod]
    public void Private_key_blocks_in_values_are_redacted()
    {
        var block = "-----BEGIN OPENSSH PRIVATE KEY-----\nAAAAB3NzaC1\n-----END OPENSSH PRIVATE KEY-----";
        var details = new Dictionary<string, string> { ["note"] = $"used key {block} for copy" };

        var sanitized = _sanitizer.SanitizeDetails(details);

        sanitized["note"].Should().NotContain("AAAAB3NzaC1").And.Contain(OperationSanitizer.Redacted);
    }

    [TestMethod]
    public void Unterminated_private_key_block_is_redacted()
    {
        var details = new Dictionary<string, string> { ["note"] = "-----BEGIN RSA PRIVATE KEY-----\nMIIEow" };

        var sanitized = _sanitizer.SanitizeDetails(details);

        sanitized["note"].Should().NotContain("MIIEow");
    }

    [TestMethod]
    public void Inline_secret_assignments_are_redacted()
    {
        var text = _sanitizer.SanitizeText("login failed password=hunter2 for user");

        text.Should().NotContain("hunter2").And.Contain("password=[redacted]");
    }

    [TestMethod]
    [DataRow("output")]
    [DataRow("stdout")]
    [DataRow("terminalOutput")]
    public void Terminal_output_keys_are_dropped(string key)
    {
        var details = new Dictionary<string, string> { [key] = "ls -la", ["path"] = "/tmp" };

        var sanitized = _sanitizer.SanitizeDetails(details);

        sanitized.Should().NotContainKey(key);
        sanitized.Should().ContainKey("path");
    }

    [TestMethod]
    public void Long_values_and_entry_counts_are_bounded()
    {
        var details = new Dictionary<string, string>();
        for (var i = 0; i < OperationSanitizer.MaxEntries + 10; i++)
        {
            details[$"key{i}"] = new string('x', OperationSanitizer.MaxValueLength * 2);
        }

        var sanitized = _sanitizer.SanitizeDetails(details);

        sanitized.Should().HaveCount(OperationSanitizer.MaxEntries);
        sanitized.Values.Should().OnlyContain(value => value.Length == OperationSanitizer.MaxValueLength);
    }

    [TestMethod]
    public void Null_details_produce_empty_dictionary()
    {
        _sanitizer.SanitizeDetails(null).Should().BeEmpty();
        _sanitizer.SanitizeText(null).Should().BeNull();
    }
}
