using DTech.Core.Credentials;
using DTech.Core.Editor.Sudo;

namespace DTech.Core.SavedConnections;

public sealed class ConnectionSecretCleaner(ICredentialStore credentialStore)
{
    public async Task<CredentialError?> RemoveAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        var passwordError = await RemoveAsync(PasswordCredentialKey.For(connectionId), cancellationToken);
        var sudoError = await RemoveAsync(SudoPasswordStore.KeyFor(connectionId), cancellationToken);
        return passwordError ?? sudoError;
    }

    private async Task<CredentialError?> RemoveAsync(CredentialKey key, CancellationToken cancellationToken)
    {
        var result = await credentialStore.Delete(key, cancellationToken);
        return result.IsSuccess || result.Error.Code == CredentialErrorCode.NotFound ? null : result.Error;
    }
}
