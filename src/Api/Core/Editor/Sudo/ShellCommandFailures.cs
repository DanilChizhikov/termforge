using System.Globalization;
using DTech.Core.Connections;

namespace DTech.Core.Editor.Sudo;

public static class ShellCommandFailures
{
    private static readonly string[] NotFoundMarkers = ["No such file or directory"];
    private static readonly string[] PermissionMarkers = ["Permission denied", "Operation not permitted", "Read-only file system"];
    private static readonly string[] ExistsMarkers = ["File exists"];

    public static SshError Describe(RemoteShellCommandResult result, string action)
    {
        var detail = FirstLine(result.StandardError);
        var exitCode = result.ExitCode.ToString(CultureInfo.InvariantCulture);
        var message = detail.Length == 0 ? $"{action} failed with exit code {exitCode}." : $"{action} failed: {detail}";
        var kind = Classify(result.StandardError);
        return new SshError(kind, message);
    }

    public static bool ContainsAny(string text, IReadOnlyList<string> markers)
    {
        for (var i = 0; i < markers.Count; i++)
        {
            var marker = markers[i];
            if (text.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static string FirstLine(string text)
    {
        var trimmed = text.Trim();
        var lineEnd = trimmed.IndexOf('\n', StringComparison.Ordinal);
        return lineEnd < 0 ? trimmed : trimmed[..lineEnd].TrimEnd();
    }

    private static SshErrorKind Classify(string standardError)
    {
        if (ContainsAny(standardError, NotFoundMarkers))
        {
            return SshErrorKind.RemotePathNotFound;
        }

        if (ContainsAny(standardError, PermissionMarkers))
        {
            return SshErrorKind.PermissionDenied;
        }

        return ContainsAny(standardError, ExistsMarkers) ? SshErrorKind.RemotePathExists : SshErrorKind.OperationFailed;
    }
}
