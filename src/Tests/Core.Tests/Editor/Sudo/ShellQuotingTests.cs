using DTech.Core.Editor.Sudo;
using FluentAssertions;

namespace DTech.Core.Tests.Editor.Sudo;

[TestClass]
public sealed class ShellQuotingTests
{
    [TestMethod]
    [DataRow("/etc/nginx/nginx.conf", "'/etc/nginx/nginx.conf'")]
    [DataRow("", "''")]
    [DataRow("/srv/my file.conf", "'/srv/my file.conf'")]
    [DataRow("/srv/it's.conf", "'/srv/it'\\''s.conf'")]
    [DataRow("''", "''\\'''\\'''")]
    [DataRow("/srv/$(reboot)`id`;rm -rf /", "'/srv/$(reboot)`id`;rm -rf /'")]
    [DataRow("/srv/\"quoted\" \\ back", "'/srv/\"quoted\" \\ back'")]
    [DataRow("-rf", "'-rf'")]
    public void Quote_WrapsValueInSingleQuotesAndEscapesSingleQuotes(string value, string expected)
    {
        var quoted = ShellQuoting.Quote(value);

        quoted.Should().Be(expected);
    }

    [TestMethod]
    public void Quote_ValueWithNul_IsRejected()
    {
        var quote = () => ShellQuoting.Quote("/etc/a\0b");

        quote.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void Quote_NewLine_StaysInsideQuotes()
    {
        var quoted = ShellQuoting.Quote("a\nb");

        quoted.Should().Be("'a\nb'");
    }
}
