using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace DTech.Core.Terminal.Recording;

public readonly record struct TerminalRecordingId
{
    private const int RandomByteCount = 16;
    private const int TextLength = RandomByteCount * 2;

    private TerminalRecordingId(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static TerminalRecordingId Create()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(RandomByteCount);
        var value = Convert.ToHexStringLower(randomBytes);
        return new TerminalRecordingId(value);
    }

    public static bool TryParse([NotNullWhen(true)] string? text, out TerminalRecordingId id)
    {
        id = default;
        if (text is null || text.Length != TextLength)
        {
            return false;
        }

        for (var i = 0; i < text.Length; i++)
        {
            if (!IsLowerHexDigit(text[i]))
            {
                return false;
            }
        }

        id = new TerminalRecordingId(text);
        return true;
    }

    public override string ToString()
    {
        return Value;
    }

    private static bool IsLowerHexDigit(char character)
    {
        return character is >= '0' and <= '9' or >= 'a' and <= 'f';
    }
}
