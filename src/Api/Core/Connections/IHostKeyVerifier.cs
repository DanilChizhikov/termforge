namespace DTech.Core.Connections;

public interface IHostKeyVerifier
{
    HostKeyVerification Verify(HostKeyVerificationRequest request);
}
