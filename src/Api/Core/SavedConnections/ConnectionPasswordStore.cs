using DTech.Core.Credentials;

namespace DTech.Core.SavedConnections;

public sealed class ConnectionPasswordStore(ICredentialStore credentialStore)
{
    public async Task<SavedConnectionResult> SaveAsync(Guid connectionId, string password, CancellationToken cancellationToken)
    {
        var key = PasswordCredentialKey.For(connectionId);
        using var secret = Secret.FromString(password);
        var result = await credentialStore.Store(key, secret, cancellationToken);
        if (result.IsSuccess)
        {
            return SavedConnectionResult.Success();
        }

        var error = SavedConnectionError.FromCredential(result.Error);
        return SavedConnectionResult.Failure(error);
    }

    public async Task<SavedConnectionResult> RemoveAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        var key = PasswordCredentialKey.For(connectionId);
        var result = await credentialStore.Delete(key, cancellationToken);
        if (result.IsSuccess || result.Error.Code == CredentialErrorCode.NotFound)
        {
            return SavedConnectionResult.Success();
        }

        var error = SavedConnectionError.FromCredential(result.Error);
        return SavedConnectionResult.Failure(error);
    }

    public async Task<SavedConnectionResult<string>> ReadAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        var key = PasswordCredentialKey.For(connectionId);
        var result = await credentialStore.Read(key, cancellationToken);
        if (!result.IsSuccess)
        {
            var error = SavedConnectionError.FromCredential(result.Error);
            return SavedConnectionResult<string>.Failure(error);
        }

        using var secret = result.Value;
        var password = secret.RevealString();
        return SavedConnectionResult<string>.Success(password);
    }
}
