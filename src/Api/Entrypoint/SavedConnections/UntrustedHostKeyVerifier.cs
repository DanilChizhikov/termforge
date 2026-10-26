using DTech.Core.Connections;

namespace DTech.Entrypoint.SavedConnections;

public sealed class UntrustedHostKeyVerifier : IHostKeyVerifier
{
    public HostKeyTrustDecision Verify(HostKeyVerificationRequest request)
    {
        return HostKeyTrustDecision.Reject;
    }
}
