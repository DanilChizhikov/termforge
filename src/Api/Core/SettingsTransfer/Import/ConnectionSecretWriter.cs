using DTech.Core.Credentials;
using DTech.Core.SettingsTransfer.Secrets;

namespace DTech.Core.SettingsTransfer.Import;

public sealed class ConnectionSecretWriter(ICredentialStore credentialStore)
{
    public async Task<CredentialError?> StoreAsync(ArchivedSecret secret, Guid localConnectionId, CancellationToken cancellationToken)
    {
        var key = ConnectionSecretKeys.For(localConnectionId, secret.Kind);
        var stored = await credentialStore.Store(key, secret.Value, cancellationToken);
        return stored.IsSuccess ? null : stored.Error;
    }

    public async Task<CredentialError?> RemoveAsync(Guid localConnectionId, ArchivedSecretKind kind, CancellationToken cancellationToken)
    {
        var key = ConnectionSecretKeys.For(localConnectionId, kind);
        var deleted = await credentialStore.Delete(key, cancellationToken);
        if (deleted.IsSuccess || deleted.Error.Code == CredentialErrorCode.NotFound)
        {
            return null;
        }

        return deleted.Error;
    }
}
