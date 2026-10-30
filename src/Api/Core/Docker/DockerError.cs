using DTech.Core.Connections;
using DTech.Core.Safety;

namespace DTech.Core.Docker;

public sealed record DockerError(DockerErrorCode Code, string Message)
{
    public SshErrorKind? SshErrorKind { get; init; }

    public static DockerError FromSsh(SshError error)
    {
        return new DockerError(DockerErrorCode.ConnectionFailed, error.Message)
        {
            SshErrorKind = error.Kind,
        };
    }

    public static DockerError ReadOnlyServer()
    {
        return new DockerError(DockerErrorCode.ReadOnlyServer, ServerWriteGate.ReadOnlyMessage);
    }

    public static DockerError InvalidContainer()
    {
        return new DockerError(DockerErrorCode.InvalidRequest, "The container name or id is invalid. Use letters, digits, '_', '.' or '-' (up to 128 characters).");
    }

    public static DockerError InvalidImage()
    {
        return new DockerError(DockerErrorCode.InvalidRequest, "The image reference is invalid. Use repository[:tag][@digest] with letters, digits, '.', '_', '-', '/', ':' or '@'.");
    }

    public static DockerError InvalidComposeProject()
    {
        return new DockerError(DockerErrorCode.InvalidRequest, "The compose project name is invalid. Use lowercase letters, digits, '_' or '-'.");
    }
}
