using DTech.Core.Connections;

namespace DTech.Ssh.IntegrationTests.Harness;

internal sealed record SshServerIdentity(string NetworkAlias, string HostName, string PrivateKeyPath)
{
    public required HostKey HostKey { get; init; }

    public required string HostPublicKeyLine { get; init; }
}

internal sealed record SshServerIdentityRequest(string NetworkAlias, string KeyDirectory);
