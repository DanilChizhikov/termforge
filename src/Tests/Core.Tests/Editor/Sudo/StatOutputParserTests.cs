using DTech.Core.Editor.Sudo;
using DTech.Core.Files;
using FluentAssertions;

namespace DTech.Core.Tests.Editor.Sudo;

[TestClass]
public sealed class StatOutputParserTests
{
    [TestMethod]
    public void Parse_RegularFile_ReturnsKindModeSizeTimeAndOwner()
    {
        var attributes = StatOutputParser.Parse("81a0 1234 1767225600 33 4\n");

        attributes.Should().NotBeNull();
        attributes!.Kind.Should().Be(RemoteFileKind.File);
        attributes.Mode.Should().Be(0b110_100_000);
        attributes.Size.Should().Be(1234);
        attributes.LastWriteTimeUtc.Should().Be(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        attributes.UserId.Should().Be(33);
        attributes.GroupId.Should().Be(4);
    }

    [TestMethod]
    public void Parse_SetUserIdBit_IsKept()
    {
        var attributes = StatOutputParser.Parse("89e8 10 0 0 0");

        attributes!.Mode.Should().Be(0b100_111_101_000);
    }

    [TestMethod]
    [DataRow("41ed 4096 0 0 0", RemoteFileKind.Directory)]
    [DataRow("a1ff 7 0 0 0", RemoteFileKind.SymbolicLink)]
    [DataRow("21b6 0 0 0 0", RemoteFileKind.Other)]
    public void Parse_OtherTypes_MapKind(string output, RemoteFileKind expected)
    {
        var attributes = StatOutputParser.Parse(output);

        attributes!.Kind.Should().Be(expected);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("81a4 12 1700000000 0")]
    [DataRow("zz 12 1700000000 0 0")]
    [DataRow("81a4 -1 1700000000 0 0")]
    public void Parse_UnexpectedOutput_ReturnsNull(string output)
    {
        var attributes = StatOutputParser.Parse(output);

        attributes.Should().BeNull();
    }
}
