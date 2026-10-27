using System.Text;

namespace DTech.Core.Editor;

public sealed class TextContentCodec
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly byte[] ByteOrderMark = [0xEF, 0xBB, 0xBF];

    public EditorResult<TextContent> Decode(ReadOnlySpan<byte> bytes, string path)
    {
        var hasByteOrderMark = bytes.StartsWith(ByteOrderMark);
        var body = hasByteOrderMark ? bytes[ByteOrderMark.Length..] : bytes;
        if (body.Contains((byte)0))
        {
            var binary = EditorError.Binary(path);
            return EditorResult<TextContent>.Failure(binary);
        }

        try
        {
            var text = StrictUtf8.GetString(body);
            var content = new TextContent(text, hasByteOrderMark);
            return EditorResult<TextContent>.Success(content);
        }
        catch (DecoderFallbackException)
        {
            var invalid = EditorError.NotUtf8(path);
            return EditorResult<TextContent>.Failure(invalid);
        }
    }

    public EditorResult<byte[]> Encode(TextContent content)
    {
        byte[] body;
        try
        {
            body = StrictUtf8.GetBytes(content.Text);
        }
        catch (EncoderFallbackException)
        {
            var invalid = EditorError.Invalid("The text contains characters that cannot be encoded as UTF-8.");
            return EditorResult<byte[]>.Failure(invalid);
        }

        if (!content.HasByteOrderMark)
        {
            return EditorResult<byte[]>.Success(body);
        }

        var bytes = new byte[ByteOrderMark.Length + body.Length];
        ByteOrderMark.CopyTo(bytes, 0);
        body.CopyTo(bytes, ByteOrderMark.Length);
        return EditorResult<byte[]>.Success(bytes);
    }
}
