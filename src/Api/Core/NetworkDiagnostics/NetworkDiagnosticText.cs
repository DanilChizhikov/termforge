using System.Text;

namespace DTech.Core.NetworkDiagnostics;

internal static class NetworkDiagnosticText
{
    private const string Truncated = "\n[Output truncated]";

    public static string Format(string? value, int maxLength)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxLength, Truncated.Length);
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var text = new StringBuilder(Math.Min(value.Length, maxLength));
        for (var i = 0; i < value.Length; i++)
        {
            var character = value[i];
            if (character == '\r')
            {
                if (i + 1 < value.Length && value[i + 1] == '\n') continue;
                character = '\n';
            }
            if (char.IsControl(character) && character != '\n') continue;
            if (text.Length == maxLength)
            {
                text.Length = maxLength - Truncated.Length;
                if (text.Length > 0 && char.IsHighSurrogate(text[^1])) text.Length--;
                text.Append(Truncated);
                return text.ToString();
            }
            text.Append(character);
        }
        return text.ToString().Trim();
    }
}
