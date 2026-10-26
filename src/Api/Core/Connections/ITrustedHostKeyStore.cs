namespace DTech.Core.Connections;

public interface ITrustedHostKeyStore
{
    IReadOnlyList<HostKey> GetTrustedHostKeys(SshEndpoint endpoint);

    Task AddAsync(HostKeyVerificationRequest request, CancellationToken cancellationToken);

    Task ReplaceAsync(HostKeyVerificationRequest request, CancellationToken cancellationToken);
}
