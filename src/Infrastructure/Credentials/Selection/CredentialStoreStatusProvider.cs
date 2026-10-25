using DTech.Core.Credentials;
using DTech.Credentials.Vault;

namespace DTech.Credentials.Selection;

public sealed class CredentialStoreStatusProvider(CredentialBackendSelector selector, EncryptedVault vault) : ICredentialStoreStatusProvider
{
    public async Task<CredentialStoreStatus> GetStatus(CancellationToken cancellationToken)
    {
        var selection = await selector.Select(cancellationToken);
        return new CredentialStoreStatus(selection.Backend, selection.Reason, selection.OperatingSystemStore, vault.State);
    }
}
