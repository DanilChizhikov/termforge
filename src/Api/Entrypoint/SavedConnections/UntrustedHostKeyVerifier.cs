using DTech.Core.Connections;

namespace DTech.Entrypoint.SavedConnections;

public sealed class UntrustedHostKeyVerifier : IHostKeyVerifier
{
    public HostKeyVerification Verify(HostKeyVerificationRequest request)
    {
        return new HostKeyVerification(HostKeyStatus.Unknown, []);
    }
}
