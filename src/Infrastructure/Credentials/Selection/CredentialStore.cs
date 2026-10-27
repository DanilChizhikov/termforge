using DTech.Core.Credentials;

namespace DTech.Credentials.Selection;

public sealed class CredentialStore(CredentialBackendSelector selector) : ICredentialStore
{
    public async Task<CredentialResult> Store(CredentialKey key, Secret secret, CancellationToken cancellationToken)
    {
        var selection = await selector.Select(cancellationToken);
        return await selection.Store.Store(key, secret, cancellationToken);
    }

    public async Task<CredentialResult<Secret>> Read(CredentialKey key, CancellationToken cancellationToken)
    {
        var selection = await selector.Select(cancellationToken);
        return await selection.Store.Read(key, cancellationToken);
    }

    public async Task<CredentialResult> Delete(CredentialKey key, CancellationToken cancellationToken)
    {
        var selection = await selector.Select(cancellationToken);
        return await selection.Store.Delete(key, cancellationToken);
    }
}
