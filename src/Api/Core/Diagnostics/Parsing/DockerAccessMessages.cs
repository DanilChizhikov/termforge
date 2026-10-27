namespace DTech.Core.Diagnostics.Parsing;

public static class DockerAccessMessages
{
    public const string NotInstalled = "Docker is not installed on the server.";
    public const string PermissionDenied = "Permission denied: the user is not in the docker group. Add the user to the docker group (then log in again) or use rootless Docker.";
    public const string DaemonNotRunning = "The Docker daemon is not running or its socket is unreachable. Start the docker service on the server.";
}
