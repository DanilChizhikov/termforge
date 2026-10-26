using DTech.Core.Editor;
using FluentAssertions;

namespace DTech.Core.Tests.Editor;

[TestClass]
public sealed class TextContentCodecTests
{
    private const string Path = "/etc/app.conf";

    private readonly TextContentCodec _codec = new();

    [TestMethod]
    public void Decode_Utf8WithByteOrderMark_RoundTripsMark()
    {
        byte[] bytes = [0xEF, 0xBB, 0xBF, (byte)'a', (byte)'=', (byte)'1'];

        var decoded = _codec.Decode(bytes, Path);
        var encoded = _codec.Encode(decoded.Value);

        decoded.Value.Text.Should().Be("a=1");
        decoded.Value.HasByteOrderMark.Should().BeTrue();
        encoded.Value.Should().Equal(bytes);
    }

    [TestMethod]
    public void Decode_ContentWithNulByte_IsBinary()
    {
        byte[] bytes = [0x7F, (byte)'E', (byte)'L', (byte)'F', 0x00, 0x01];

        var decoded = _codec.Decode(bytes, Path);

        decoded.Error!.Code.Should().Be(EditorErrorCode.Binary);
    }

    [TestMethod]
    public void Decode_InvalidUtf8_IsRejected()
    {
        byte[] bytes = [(byte)'a', 0xC3, 0x28];

        var decoded = _codec.Decode(bytes, Path);

        decoded.Error!.Code.Should().Be(EditorErrorCode.Binary);
        decoded.Error.Message.Should().Contain("UTF-8");
    }

    [TestMethod]
    public void Encode_TextWithoutMark_KeepsLineEndings()
    {
        var content = new TextContent("a\r\nb\n", false);

        var encoded = _codec.Encode(content);

        encoded.Value.Should().Equal((byte)'a', (byte)'\r', (byte)'\n', (byte)'b', (byte)'\n');
    }
}
