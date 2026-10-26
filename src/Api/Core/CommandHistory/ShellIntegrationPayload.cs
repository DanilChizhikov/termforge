using System.Globalization;
using System.Text;

namespace DTech.Core.CommandHistory;

internal static class ShellIntegrationPayload
{
    private const string FinalTermPrefix = "133;";
    private const string VsCodePrefix = "633;";
    private const char CommandFinishedCode = 'D';
    private const char CommandLineCode = 'E';

    public static ShellIntegrationEvent? Parse(string payload)
    {
        var body = StripPrefix(payload);
        if (body is null || body.Length == 0)
        {
            return null;
        }

        var code = body[0];
        if (code == CommandFinishedCode)
        {
            return ParseFinished(body);
        }

        if (code == CommandLineCode && body.Length > 1 && body[1] == ';')
        {
            return ParseCommandLine(body);
        }

        return null;
    }

    public static string Unescape(string text)
    {
        var bytes = new List<byte>(text.Length);
        var buffer = new byte[4];
        var i = 0;
        while (i < text.Length)
        {
            var character = text[i];
            if (character == '\\' && i + 1 < text.Length && text[i + 1] == '\\')
            {
                bytes.Add((byte)'\\');
                i += 2;
            }
            else if (character == '\\' && i + 3 < text.Length && text[i + 1] == 'x' && TryReadHexByte(text, i + 2, out var value))
            {
                bytes.Add(value);
                i += 4;
            }
            else
            {
                var length = char.IsHighSurrogate(character) && i + 1 < text.Length ? 2 : 1;
                var written = Encoding.UTF8.GetBytes(text.AsSpan(i, length), buffer);
                for (var b = 0; b < written; b++)
                {
                    bytes.Add(buffer[b]);
                }

                i += length;
            }
        }

        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    private static string? StripPrefix(string payload)
    {
        if (payload.StartsWith(FinalTermPrefix, StringComparison.Ordinal))
        {
            return payload[FinalTermPrefix.Length..];
        }

        if (payload.StartsWith(VsCodePrefix, StringComparison.Ordinal))
        {
            return payload[VsCodePrefix.Length..];
        }

        return null;
    }

    private static ShellIntegrationEvent ParseFinished(string body)
    {
        int? exitCode = null;
        if (body.Length > 2 && body[1] == ';')
        {
            var field = body[2..];
            var end = field.IndexOf(';');
            var text = end < 0 ? field : field[..end];
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                exitCode = parsed;
            }
        }

        return ShellIntegrationEvent.ForCommandFinished(exitCode);
    }

    private static ShellIntegrationEvent ParseCommandLine(string body)
    {
        var field = body[2..];
        var end = field.IndexOf(';');
        var escaped = end < 0 ? field : field[..end];
        var commandLine = Unescape(escaped);
        return ShellIntegrationEvent.ForCommandLine(commandLine);
    }

    private static bool TryReadHexByte(string text, int start, out byte value)
    {
        var hex = text.AsSpan(start, 2);
        return byte.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value);
    }
}
