namespace DTech.Core.Diagnostics;

public static class ShellScript
{
    public static string Wrap(string script)
    {
        var quotedScript = ShellArgument.Quote(script);
        return $"sh -c {quotedScript}";
    }
}
