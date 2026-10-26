using DTech.Core.Connections;
using DTech.HostKeys.Decisions;

namespace DTech.Entrypoint.HostKeys;

internal static class PendingHostKeyResponseMapper
{
    public static IReadOnlyList<PendingHostKeyResponse> Map(IReadOnlyList<PendingHostKeyDecision> pendingDecisions)
    {
        var responses = new List<PendingHostKeyResponse>(pendingDecisions.Count);
        for (var i = 0; i < pendingDecisions.Count; i++)
        {
            var pendingDecision = pendingDecisions[i];
            var response = Map(pendingDecision);
            responses.Add(response);
        }

        return responses;
    }

    private static PendingHostKeyResponse Map(PendingHostKeyDecision pendingDecision)
    {
        var challenge = pendingDecision.Challenge;
        var endpoint = challenge.Request.Endpoint;
        var status = challenge.Verification.Status.ToString().ToLowerInvariant();
        var presentedKey = Map(challenge.Request.HostKey);
        var knownKeys = Map(challenge.Verification.KnownHostKeys);
        return new PendingHostKeyResponse(pendingDecision.Id, endpoint.Host, endpoint.Port, status)
        {
            PresentedKey = presentedKey,
            KnownKeys = knownKeys,
            ExpiresAt = pendingDecision.ExpiresAt,
        };
    }

    private static IReadOnlyList<HostKeyResponse> Map(IReadOnlyList<HostKey> hostKeys)
    {
        var responses = new List<HostKeyResponse>(hostKeys.Count);
        for (var i = 0; i < hostKeys.Count; i++)
        {
            var hostKey = hostKeys[i];
            var response = Map(hostKey);
            responses.Add(response);
        }

        return responses;
    }

    private static HostKeyResponse Map(HostKey hostKey)
    {
        return new HostKeyResponse(hostKey.Algorithm, hostKey.Sha256Fingerprint);
    }
}
