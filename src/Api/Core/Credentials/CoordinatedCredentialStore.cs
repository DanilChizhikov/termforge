namespace DTech.Core.Credentials;

public sealed class CoordinatedCredentialStore(IUncoordinatedCredentialStore store, CredentialMutationCoordinator coordinator) : ICredentialStore
{
    public async Task<CredentialResult> Store(CredentialKey key, Secret secret, CancellationToken cancellationToken)
    {
        using var lease = await coordinator.AcquireAsync(cancellationToken);
        return await store.Store(key, secret, cancellationToken);
    }

    public async Task<CredentialResult<Secret>> Read(CredentialKey key, CancellationToken cancellationToken)
    {
        using var lease = await coordinator.AcquireAsync(cancellationToken);
        return await store.Read(key, cancellationToken);
    }

    public async Task<CredentialResult> Delete(CredentialKey key, CancellationToken cancellationToken)
    {
        using var lease = await coordinator.AcquireAsync(cancellationToken);
        return await store.Delete(key, cancellationToken);
    }
}
