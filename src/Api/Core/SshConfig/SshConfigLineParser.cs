using System.Text;

namespace DTech.Core.SshConfig;

public static class SshConfigLineParser
{
    public static SshConfigDirective? Parse(string filePath, int lineNumber, string line)
    {
        var text = line.AsSpan().Trim();
        if (text.IsEmpty || text[0] == '#')
        {
            return null;
        }

        var keywordEnd = 0;
        while (keywordEnd < text.Length && !char.IsWhiteSpace(text[keywordEnd]) && text[keywordEnd] != '=')
        {
            keywordEnd++;
        }

        var keyword = text[..keywordEnd].ToString();
        var rest = text[keywordEnd..].TrimStart();
        if (!rest.IsEmpty && rest[0] == '=')
        {
            rest = rest[1..].TrimStart();
        }

        var values = SplitValues(rest);
        return new SshConfigDirective(filePath, lineNumber, keyword, values);
    }

    private static List<string> SplitValues(ReadOnlySpan<char> text)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        var hasToken = false;
        for (var i = 0; i < text.Length; i++)
        {
            var character = text[i];
            if (character == '"')
            {
                inQuotes = !inQuotes;
                hasToken = true;
                continue;
            }

            if (!inQuotes && character == '#' && !hasToken)
            {
                break;
            }

            if (!inQuotes && char.IsWhiteSpace(character))
            {
                FlushToken(values, current, ref hasToken);
                continue;
            }

            current.Append(character);
            hasToken = true;
        }

        FlushToken(values, current, ref hasToken);
        return values;
    }

    private static void FlushToken(List<string> values, StringBuilder current, ref bool hasToken)
    {
        if (hasToken)
        {
            values.Add(current.ToString());
        }

        current.Clear();
        hasToken = false;
    }
}
