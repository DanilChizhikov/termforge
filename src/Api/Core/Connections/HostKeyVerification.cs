namespace DTech.Core.Connections;

public sealed record HostKeyVerification(HostKeyStatus Status, IReadOnlyList<HostKey> KnownHostKeys)
{
    public bool IsTrusted => Status == HostKeyStatus.Trusted;
}
