namespace DTech.Core.Diagnostics;

public static class ShellArgument
{
    public static string Quote(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Contains('\0'))
        {
            throw new ArgumentException("A shell argument cannot contain a NUL character.", nameof(value));
        }

        var escaped = value.Replace("'", "'\"'\"'", StringComparison.Ordinal);
        return $"'{escaped}'";
    }
}
