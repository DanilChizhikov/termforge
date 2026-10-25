namespace DTech.Core.Credentials;

public interface ICredentialVault
{
    CredentialVaultState State { get; }

    Task<CredentialResult> Unlock(Secret masterPassword, CancellationToken cancellationToken);

    void Lock();
}
