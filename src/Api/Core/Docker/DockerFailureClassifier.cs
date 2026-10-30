using System.Globalization;
using DTech.Core.Diagnostics;
using DTech.Core.Diagnostics.Parsing;

namespace DTech.Core.Docker;

public static class DockerFailureClassifier
{
    private const int CommandNotFoundExitCode = 127;
    private const int DetailMaxLength = 300;

    public static DockerError Classify(RemoteCommandResult result)
    {
        var text = result.CombinedOutput.ToLowerInvariant();
        if (IsDockerMissing(result.ExitCode, text))
        {
            return new DockerError(DockerErrorCode.DockerMissing, DockerAccessMessages.NotInstalled);
        }

        var access = DockerAccessParser.Parse(result);
        if (access.Access == DockerAccess.PermissionDenied)
        {
            return new DockerError(DockerErrorCode.PermissionDenied, DockerAccessMessages.PermissionDenied);
        }

        if (access.Access == DockerAccess.DaemonNotRunning)
        {
            return new DockerError(DockerErrorCode.DaemonUnavailable, DockerAccessMessages.DaemonNotRunning);
        }

        if (text.Contains("no such container", StringComparison.Ordinal))
        {
            return new DockerError(DockerErrorCode.ContainerNotFound, "The container does not exist on the server. Refresh the container list.");
        }

        if (text.Contains("no such image", StringComparison.Ordinal))
        {
            return new DockerError(DockerErrorCode.ImageNotFound, "The image does not exist on the server. Refresh the image list.");
        }

        if (IsImageInUse(text))
        {
            return new DockerError(DockerErrorCode.ImageInUse, "The image is used by a container. Remove the container first.");
        }

        if (IsComposeMissing(text))
        {
            return new DockerError(DockerErrorCode.ComposeMissing, "Docker Compose v2 is not installed on the server. Install the docker compose plugin.");
        }

        return new DockerError(DockerErrorCode.CommandFailed, DescribeCommandFailure(result));
    }

    private static bool IsDockerMissing(int exitCode, string text)
    {
        var mentionsMissingDocker = text.Contains("docker: not found", StringComparison.Ordinal)
            || text.Contains("docker: command not found", StringComparison.Ordinal);
        return exitCode == CommandNotFoundExitCode || mentionsMissingDocker;
    }

    private static bool IsImageInUse(string text)
    {
        return text.Contains("image is being used by", StringComparison.Ordinal)
            || text.Contains("conflict: unable to remove", StringComparison.Ordinal)
            || text.Contains("conflict: unable to delete", StringComparison.Ordinal);
    }

    private static bool IsComposeMissing(string text)
    {
        return text.Contains("'compose' is not a docker command", StringComparison.Ordinal)
            || text.Contains("unknown command \"compose\"", StringComparison.Ordinal)
            || text.Contains("unknown command: docker compose", StringComparison.Ordinal);
    }

    private static string DescribeCommandFailure(RemoteCommandResult result)
    {
        var exitCode = result.ExitCode.ToString(CultureInfo.InvariantCulture);
        var output = string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput : result.StandardError;
        var detail = RemoteText.Sanitize(output, DetailMaxLength);
        return detail.Length == 0 ? $"Docker exited with code {exitCode}." : $"Docker exited with code {exitCode}: {detail}";
    }
}
