namespace DTech.Core.Connections;

public interface IHostKeyVerifier
{
    IReadOnlyList<HostKey> GetKnownHostKeys(SshEndpoint endpoint) => [];

    HostKeyVerification Verify(HostKeyVerificationRequest request);
}
