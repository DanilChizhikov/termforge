namespace DTech.Core.Diagnostics;

public static class ShellArgument
{
    public static string Quote(string value)
    {
        var escaped = value.Replace("'", "'\\''", StringComparison.Ordinal);
        return $"'{escaped}'";
    }
}
