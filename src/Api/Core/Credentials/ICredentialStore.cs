namespace DTech.Core.Credentials;

public interface ICredentialStore
{
    Task<CredentialResult> Store(CredentialKey key, Secret secret, CancellationToken cancellationToken);

    Task<CredentialResult<Secret>> Read(CredentialKey key, CancellationToken cancellationToken);

    Task<CredentialResult> Delete(CredentialKey key, CancellationToken cancellationToken);
}
