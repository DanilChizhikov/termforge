using System.Text.RegularExpressions;

namespace DTech.Core.Diagnostics.Parsing;

public static partial class DockerAccessParser
{
    public const string Script = "docker info --format '{{.ServerVersion}}' 2>&1";

    public static DockerAccessInfo Parse(RemoteCommandResult result)
    {
        var text = result.CombinedOutput.ToLowerInvariant();
        if (text.Contains("permission denied", StringComparison.Ordinal))
        {
            return new DockerAccessInfo(DockerAccess.PermissionDenied, null);
        }

        var daemonDown = text.Contains("cannot connect to the docker daemon", StringComparison.Ordinal)
            || text.Contains("is the docker daemon running", StringComparison.Ordinal);
        if (daemonDown)
        {
            return new DockerAccessInfo(DockerAccess.DaemonNotRunning, null);
        }

        var version = result.Succeeded ? FindVersion(result.StandardOutput) : null;
        return version is null ? new DockerAccessInfo(DockerAccess.Unknown, null) : new DockerAccessInfo(DockerAccess.Accessible, version);
    }

    private static string? FindVersion(string standardOutput)
    {
        var lines = RemoteText.SplitLines(standardOutput);
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            return VersionPattern().IsMatch(line) ? line : null;
        }

        return null;
    }

    [GeneratedRegex("^[0-9A-Za-z][0-9A-Za-z.+_-]{0,39}$")]
    private static partial Regex VersionPattern();
}
