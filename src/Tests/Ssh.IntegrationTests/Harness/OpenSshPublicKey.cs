using System.Security.Cryptography;
using DTech.Core.Connections;

namespace DTech.Ssh.IntegrationTests.Harness;

internal static class OpenSshPublicKey
{
    public static HostKey ToHostKey(string publicKeyLine)
    {
        var parts = publicKeyLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var algorithm = parts[0];
        var keyBlob = Convert.FromBase64String(parts[1]);
        var digest = SHA256.HashData(keyBlob);
        var encodedDigest = Convert.ToBase64String(digest).TrimEnd('=');
        return new HostKey(algorithm, $"SHA256:{encodedDigest}");
    }

    public static string ToKnownHostsLine(string publicKeyLine, SshEndpoint endpoint)
    {
        var parts = publicKeyLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return $"[{endpoint.Host}]:{endpoint.Port} {parts[0]} {parts[1]}";
    }
}
