using Microsoft.Extensions.Options;

namespace DTech.HostKeys.KnownHosts;

internal sealed class KnownHostsFiles(IOptions<HostKeyVerificationOptions> options)
{
    private const string HomePrefix = "~";

    public static string DefaultPath => Path.Combine(GetHomeDirectory(), ".ssh", "known_hosts");

    public IReadOnlyList<string> Resolve()
    {
        var configuredPaths = options.Value.KnownHostsFiles;
        if (configuredPaths.Count == 0)
        {
            return [DefaultPath];
        }

        var resolvedPaths = new List<string>(configuredPaths.Count);
        for (var i = 0; i < configuredPaths.Count; i++)
        {
            var configuredPath = configuredPaths[i];
            var resolvedPath = ExpandHome(configuredPath);
            resolvedPaths.Add(resolvedPath);
        }

        return resolvedPaths;
    }

    private static string ExpandHome(string path)
    {
        if (path == HomePrefix)
        {
            return GetHomeDirectory();
        }

        var startsWithHome = path.StartsWith("~/", StringComparison.Ordinal) || path.StartsWith("~\\", StringComparison.Ordinal);
        if (!startsWithHome)
        {
            return path;
        }

        var relativePath = path[2..];
        return Path.Combine(GetHomeDirectory(), relativePath);
    }

    private static string GetHomeDirectory()
    {
        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }
}
