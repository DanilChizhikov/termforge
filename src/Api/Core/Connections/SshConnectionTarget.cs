namespace DTech.Core.Connections;

public sealed record SshConnectionTarget(SshEndpoint Endpoint, string UserName, SshAuthentication Authentication)
{
    public SshConnectionTarget? JumpHost { get; init; }
}
