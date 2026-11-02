using DTech.Core.Credentials;

namespace DTech.Credentials.Selection;

public sealed class CredentialBackendStore(CredentialBackendSelector selector) : IUncoordinatedCredentialStore
{
    public async Task<CredentialResult> Store(CredentialKey key, Secret secret, CancellationToken cancellationToken)
    {
        var selection = await selector.Select(cancellationToken);
        return await selection.Store.Store(key, secret, cancellationToken);
    }

    public async Task<CredentialResult<Secret>> Read(CredentialKey key, CancellationToken cancellationToken)
    {
        var selection = await selector.Select(cancellationToken);
        var result = await selection.Store.Read(key, cancellationToken);
        if (!result.IsSuccess && result.Error.Code == CredentialErrorCode.NotFound &&
            selection.Backend == CredentialBackend.EncryptedVault && !selection.OperatingSystemStore.IsAvailable)
        {
            var error = CredentialErrors.StoreUnavailable($"The credential is absent from the selected vault, while the operating system credential store is unavailable. {selection.Reason}");
            return CredentialResult<Secret>.Failure(error);
        }

        return result;
    }

    public async Task<CredentialResult> Delete(CredentialKey key, CancellationToken cancellationToken)
    {
        var selection = await selector.Select(cancellationToken);
        return await selection.Store.Delete(key, cancellationToken);
    }
}
