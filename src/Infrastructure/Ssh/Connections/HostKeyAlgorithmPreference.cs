using DTech.Core.Connections;
using Renci.SshNet;

namespace DTech.Ssh.Connections;

internal static class HostKeyAlgorithmPreference
{
    public static void Apply(ConnectionInfo connectionInfo, IReadOnlyList<HostKey> knownKeys)
    {
        if (knownKeys.Count == 0)
        {
            return;
        }

        var algorithms = new HashSet<string>(knownKeys.Select(key => key.Algorithm), StringComparer.Ordinal);
        if (algorithms.Contains("ssh-rsa"))
        {
            algorithms.Add("rsa-sha2-256");
            algorithms.Add("rsa-sha2-512");
        }

        var ordered = connectionInfo.HostKeyAlgorithms.OrderByDescending(pair => algorithms.Contains(pair.Key)).ToArray();
        connectionInfo.HostKeyAlgorithms.Clear();
        foreach (var pair in ordered)
        {
            connectionInfo.HostKeyAlgorithms.Add(pair.Key, pair.Value);
        }
    }
}
