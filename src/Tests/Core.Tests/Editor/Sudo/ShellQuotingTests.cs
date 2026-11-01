using DTech.Core.Diagnostics;
using FluentAssertions;

namespace DTech.Core.Tests.Editor.Sudo;

[TestClass]
public sealed class ShellQuotingTests
{
    [TestMethod]
    [DataRow("/etc/nginx/nginx.conf", "'/etc/nginx/nginx.conf'")]
    [DataRow("", "''")]
    [DataRow("/srv/my file.conf", "'/srv/my file.conf'")]
    [DataRow("/srv/it's.conf", "'/srv/it'\"'\"'s.conf'")]
    [DataRow("''", "''\"'\"''\"'\"''")]
    [DataRow("/srv/$(reboot)`id`;rm -rf /", "'/srv/$(reboot)`id`;rm -rf /'")]
    [DataRow("/srv/\"quoted\" \\ back", "'/srv/\"quoted\" \\ back'")]
    [DataRow("-rf", "'-rf'")]
    public void Quote_WrapsValueInSingleQuotesAndEscapesSingleQuotes(string value, string expected)
    {
        var quoted = ShellArgument.Quote(value);

        quoted.Should().Be(expected);
    }

    [TestMethod]
    public void Quote_ValueWithNul_IsRejected()
    {
        var quote = () => ShellArgument.Quote("/etc/a\0b");

        quote.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void Quote_NewLine_StaysInsideQuotes()
    {
        var quoted = ShellArgument.Quote("a\nb");

        quoted.Should().Be("'a\nb'");
    }

    [TestMethod]
    public void Quote_Null_IsRejected()
    {
        var quote = () => ShellArgument.Quote(null!);

        quote.Should().Throw<ArgumentNullException>();
    }
}
