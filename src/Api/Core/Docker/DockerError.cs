using DTech.Core.Connections;

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

    public static DockerError InvalidContainer()
    {
        return new DockerError(DockerErrorCode.InvalidRequest, "The container name or id is invalid. Use letters, digits, '_', '.' or '-' (up to 128 characters).");
    }
}
