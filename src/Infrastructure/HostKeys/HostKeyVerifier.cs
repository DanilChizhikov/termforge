using DTech.Core.Connections;
using DTech.HostKeys.KnownHosts;

namespace DTech.HostKeys;

internal sealed class HostKeyVerifier(KnownHostsReader knownHostsReader, ITrustedHostKeyStore trustedHostKeyStore) : IHostKeyVerifier
{
    public HostKeyVerification Verify(HostKeyVerificationRequest request)
    {
        var knownHostKeys = new List<HostKey>();
        var isRevoked = CollectKnownHostsKeys(request, knownHostKeys);
        if (isRevoked)
        {
            return new HostKeyVerification(HostKeyStatus.Revoked, knownHostKeys);
        }

        var trustedHostKeys = trustedHostKeyStore.GetTrustedHostKeys(request.Endpoint);
        AddDistinct(knownHostKeys, trustedHostKeys);
        var status = DetermineStatus(request.HostKey, knownHostKeys);
        return new HostKeyVerification(status, knownHostKeys);
    }

    private bool CollectKnownHostsKeys(HostKeyVerificationRequest request, List<HostKey> knownHostKeys)
    {
        var isRevoked = false;
        var entries = knownHostsReader.ReadEntries();
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry.Marker == KnownHostsMarker.CertificateAuthority || !KnownHostsHostMatcher.Matches(entry.HostPatterns, request.Endpoint))
            {
                continue;
            }

            if (entry.Marker == KnownHostsMarker.Revoked)
            {
                isRevoked |= entry.HostKey == request.HostKey;
                continue;
            }

            AddDistinct(knownHostKeys, entry.HostKey);
        }

        return isRevoked;
    }

    private static HostKeyStatus DetermineStatus(HostKey presentedHostKey, IReadOnlyList<HostKey> knownHostKeys)
    {
        var hasKeyOfSameAlgorithm = false;
        for (var i = 0; i < knownHostKeys.Count; i++)
        {
            var knownHostKey = knownHostKeys[i];
            if (knownHostKey == presentedHostKey)
            {
                return HostKeyStatus.Trusted;
            }

            hasKeyOfSameAlgorithm |= knownHostKey.Algorithm == presentedHostKey.Algorithm;
        }

        return hasKeyOfSameAlgorithm ? HostKeyStatus.Changed : HostKeyStatus.Unknown;
    }

    private static void AddDistinct(List<HostKey> knownHostKeys, IReadOnlyList<HostKey> additionalHostKeys)
    {
        for (var i = 0; i < additionalHostKeys.Count; i++)
        {
            var additionalHostKey = additionalHostKeys[i];
            AddDistinct(knownHostKeys, additionalHostKey);
        }
    }

    private static void AddDistinct(List<HostKey> knownHostKeys, HostKey hostKey)
    {
        if (!knownHostKeys.Contains(hostKey))
        {
            knownHostKeys.Add(hostKey);
        }
    }
}
