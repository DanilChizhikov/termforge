namespace DTech.Core.Diagnostics;

public static class ShellScript
{
    public static string Wrap(string script)
    {
        var escaped = script.Replace("'", "'\\''", StringComparison.Ordinal);
        return $"sh -c '{escaped}'";
    }
}
