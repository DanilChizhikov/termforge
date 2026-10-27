namespace DTech.Core.SshConfig;

public static class SshConfigPathExpander
{
    public static string Expand(string path, string homeDirectory)
    {
        if (path == "~")
        {
            return homeDirectory;
        }

        var hasHomePrefix = path.StartsWith("~/", StringComparison.Ordinal) || path.StartsWith("~\\", StringComparison.Ordinal);
        return hasHomePrefix ? Path.Combine(homeDirectory, path[2..]) : path;
    }
}
