using System.Buffers;
using System.Text.Json;

namespace DTech.Core.Terminal.Recording;

public static class AsciicastFormat
{
    public const int Version = 2;
    public const string OutputEventType = "o";
    public const string FileExtension = ".cast";
    public const string MediaType = "application/x-asciicast";

    private const byte LineFeed = (byte)'\n';
    private const int TimeDecimals = 6;

    public static byte[] Header(TerminalRecordingMetadata metadata)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", Version);
            writer.WriteNumber("width", metadata.Size.Columns);
            writer.WriteNumber("height", metadata.Size.Rows);
            var startedAt = new DateTimeOffset(metadata.StartedAt, TimeSpan.Zero);
            writer.WriteNumber("timestamp", startedAt.ToUnixTimeSeconds());
            writer.WriteString("title", $"{metadata.ConnectionName} #{metadata.SessionNumber}");
            writer.WriteStartObject("env");
            writer.WriteString("TERM", TerminalSessionRequest.DefaultTerminalType);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return Terminate(buffer);
    }

    public static byte[] OutputEvent(TimeSpan elapsed, string text)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartArray();
            var seconds = Math.Round(elapsed.TotalSeconds, TimeDecimals);
            writer.WriteNumberValue(seconds);
            writer.WriteStringValue(OutputEventType);
            writer.WriteStringValue(text);
            writer.WriteEndArray();
        }

        return Terminate(buffer);
    }

    private static byte[] Terminate(ArrayBufferWriter<byte> buffer)
    {
        var line = new byte[buffer.WrittenCount + 1];
        buffer.WrittenSpan.CopyTo(line);
        line[^1] = LineFeed;
        return line;
    }
}
