using System.Text;

namespace DTech.Core.Diagnostics;

public static class RemoteText
{
    public const int DefaultMaxLength = 120;

    public static string Sanitize(string? value, int maxLength = DefaultMaxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(Math.Min(value.Length, maxLength));
        for (var i = 0; i < value.Length && builder.Length < maxLength; i++)
        {
            var character = value[i];
            builder.Append(char.IsControl(character) ? ' ' : character);
        }

        return builder.ToString().Trim();
    }

    public static IReadOnlyList<string> SplitLines(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return [];
        }

        var normalized = value.Replace("\r", string.Empty, StringComparison.Ordinal);
        return normalized.Split('\n');
    }
}
