using System.Text;
using DTech.Core.Terminal;
using DTech.Core.Terminal.Sessions;
using FluentAssertions;

namespace DTech.Core.Tests.AiBridge;

[TestClass]
public sealed class TerminalOutputTailTests
{
    [TestMethod]
    public void Snapshot_KeepsOutputInOrder()
    {
        var tail = new TerminalOutputTail(16);

        tail.Append("hello "u8);
        tail.Append("world"u8);

        Encoding.UTF8.GetString(tail.Snapshot()).Should().Be("hello world");
    }

    [TestMethod]
    public void Snapshot_WhenFull_KeepsOnlyTheLatestBytes()
    {
        var tail = new TerminalOutputTail(8);

        tail.Append("0123456"u8);
        tail.Append("789ab"u8);

        Encoding.UTF8.GetString(tail.Snapshot()).Should().Be("456789ab");
    }

    [TestMethod]
    public void Append_LargerThanCapacity_KeepsTheEnd()
    {
        var tail = new TerminalOutputTail(4);

        tail.Append("abcdefgh"u8);

        Encoding.UTF8.GetString(tail.Snapshot()).Should().Be("efgh");
    }

    [TestMethod]
    public void Strip_RemovesColorsTitlesAndCarriageReturns()
    {
        var raw = "\u001b]0;user@host: ~\u0007\u001b[01;32muser@host\u001b[00m:~$ ls\r\nfile.txt\r\n\u001b[?2004h";

        var plain = AnsiText.Strip(raw);

        plain.Should().Be("user@host:~$ ls\nfile.txt\n");
    }

    [TestMethod]
    public void Strip_RemovesShellIntegrationSequences()
    {
        var raw = "\u001b]133;A\u001b\\$ \u001b]133;B\u0007";

        AnsiText.Strip(raw).Should().Be("$ ");
    }
}
