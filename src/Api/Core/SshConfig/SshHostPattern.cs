namespace DTech.Core.SshConfig;

public static class SshHostPattern
{
    public static bool HasWildcard(string pattern)
    {
        return pattern.Contains('*') || pattern.Contains('?') || pattern.StartsWith('!');
    }

    public static bool MatchesAny(IReadOnlyList<string> patterns, string alias)
    {
        var matched = false;
        for (var i = 0; i < patterns.Count; i++)
        {
            var pattern = patterns[i];
            if (pattern.StartsWith('!'))
            {
                if (Glob(pattern.AsSpan(1), alias))
                {
                    return false;
                }

                continue;
            }

            matched |= Glob(pattern, alias);
        }

        return matched;
    }

    private static bool Glob(ReadOnlySpan<char> pattern, ReadOnlySpan<char> text)
    {
        if (pattern.IsEmpty)
        {
            return text.IsEmpty;
        }

        if (pattern[0] == '*')
        {
            for (var skipped = 0; skipped <= text.Length; skipped++)
            {
                if (Glob(pattern[1..], text[skipped..]))
                {
                    return true;
                }
            }

            return false;
        }

        if (text.IsEmpty)
        {
            return false;
        }

        var sameCharacter = char.ToLowerInvariant(pattern[0]) == char.ToLowerInvariant(text[0]);
        return (pattern[0] == '?' || sameCharacter) && Glob(pattern[1..], text[1..]);
    }
}
