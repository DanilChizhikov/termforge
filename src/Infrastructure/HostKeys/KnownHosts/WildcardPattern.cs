namespace DTech.HostKeys.KnownHosts;

internal static class WildcardPattern
{
    private const char AnySequence = '*';
    private const char AnyCharacter = '?';

    public static bool Matches(string pattern, string value)
    {
        var patternIndex = 0;
        var valueIndex = 0;
        var starPatternIndex = -1;
        var starValueIndex = 0;
        while (valueIndex < value.Length)
        {
            if (patternIndex < pattern.Length && (pattern[patternIndex] == AnyCharacter || pattern[patternIndex] == value[valueIndex]))
            {
                patternIndex++;
                valueIndex++;
            }
            else if (patternIndex < pattern.Length && pattern[patternIndex] == AnySequence)
            {
                starPatternIndex = patternIndex;
                starValueIndex = valueIndex;
                patternIndex++;
            }
            else if (starPatternIndex >= 0)
            {
                patternIndex = starPatternIndex + 1;
                starValueIndex++;
                valueIndex = starValueIndex;
            }
            else
            {
                return false;
            }
        }

        while (patternIndex < pattern.Length && pattern[patternIndex] == AnySequence)
        {
            patternIndex++;
        }

        return patternIndex == pattern.Length;
    }
}
