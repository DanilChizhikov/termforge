namespace DTech.Core.Connections;

public sealed record SshError(SshErrorKind Kind, string Message)
{
    public HostKey? PresentedHostKey { get; init; }
}
