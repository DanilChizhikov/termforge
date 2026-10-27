namespace DTech.Core.Editor.Sudo;

public static class ShellQuoting
{
    private const string SingleQuote = "'";
    private const string EscapedSingleQuote = "'\\''";
    private const char NulCharacter = '\0';

    public static string Quote(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Contains(NulCharacter))
        {
            throw new ArgumentException("A shell argument cannot contain a NUL character.", nameof(value));
        }

        var escaped = value.Replace(SingleQuote, EscapedSingleQuote, StringComparison.Ordinal);
        return $"'{escaped}'";
    }
}
