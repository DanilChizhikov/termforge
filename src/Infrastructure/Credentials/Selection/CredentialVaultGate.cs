using DTech.Core.Credentials;
using DTech.Credentials.Vault;

namespace DTech.Credentials.Selection;

public sealed class CredentialVaultGate(CredentialBackendSelector selector, EncryptedVault vault) : ICredentialVault
{
    public CredentialVaultState State => vault.State;

    public async Task<CredentialResult> Unlock(Secret masterPassword, CancellationToken cancellationToken)
    {
        var selection = await selector.Select(cancellationToken);
        if (selection.Backend != CredentialBackend.EncryptedVault)
        {
            var error = CredentialErrors.VaultNotInUse();
            return CredentialResult.Failure(error);
        }

        return await vault.Unlock(masterPassword, cancellationToken);
    }

    public void Lock()
    {
        vault.Lock();
    }
}
