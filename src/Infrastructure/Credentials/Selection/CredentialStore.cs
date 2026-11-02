using DTech.Core.Credentials;

namespace DTech.Credentials.Selection;

public sealed class CredentialStore : ICredentialStore, IDisposable
{
    private readonly CoordinatedCredentialStore _store;
    private readonly CredentialMutationCoordinator? _ownedCoordinator;

    public CredentialStore(CoordinatedCredentialStore store)
    {
        _store = store;
    }

    public CredentialStore(CredentialBackendSelector selector)
    {
        var backend = new CredentialBackendStore(selector);
        _ownedCoordinator = new CredentialMutationCoordinator();
        _store = new CoordinatedCredentialStore(backend, _ownedCoordinator);
    }

    public Task<CredentialResult> Store(CredentialKey key, Secret secret, CancellationToken cancellationToken)
    {
        return _store.Store(key, secret, cancellationToken);
    }

    public Task<CredentialResult<Secret>> Read(CredentialKey key, CancellationToken cancellationToken)
    {
        return _store.Read(key, cancellationToken);
    }

    public Task<CredentialResult> Delete(CredentialKey key, CancellationToken cancellationToken)
    {
        return _store.Delete(key, cancellationToken);
    }

    public void Dispose()
    {
        _ownedCoordinator?.Dispose();
    }
}
