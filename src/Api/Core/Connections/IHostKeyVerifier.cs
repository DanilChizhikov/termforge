namespace DTech.Core.Connections;

public interface IHostKeyVerifier
{
    HostKeyTrustDecision Verify(HostKeyVerificationRequest request);
}
