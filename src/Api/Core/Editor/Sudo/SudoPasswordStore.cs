using DTech.Core.Credentials;

namespace DTech.Core.Editor.Sudo;

public sealed class SudoPasswordStore(ICredentialStore credentialStore)
{
    public static CredentialKey KeyFor(Guid connectionId)
    {
        var result = CredentialKey.Create($"connection:{connectionId:D}:sudo-password");
        return result.Value;
    }

    public async Task<EditorResult<Secret?>> ReadAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        var key = KeyFor(connectionId);
        var read = await credentialStore.Read(key, cancellationToken);
        if (read.IsSuccess)
        {
            return EditorResult<Secret?>.Success(read.Value);
        }

        if (read.Error.Code == CredentialErrorCode.NotFound)
        {
            return EditorResult<Secret?>.Success(null);
        }

        var failure = new EditorError(EditorErrorCode.Failed, $"The saved sudo password could not be read: {read.Error.Message}");
        return EditorResult<Secret?>.Failure(failure);
    }

    public async Task<EditorError?> SaveAsync(Guid connectionId, Secret password, CancellationToken cancellationToken)
    {
        var key = KeyFor(connectionId);
        var stored = await credentialStore.Store(key, password, cancellationToken);
        return stored.IsSuccess ? null : new EditorError(EditorErrorCode.Failed, $"The sudo password could not be saved: {stored.Error.Message}");
    }

    public async Task<EditorError?> DeleteAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        var key = KeyFor(connectionId);
        var deleted = await credentialStore.Delete(key, cancellationToken);
        if (deleted.IsSuccess || deleted.Error.Code == CredentialErrorCode.NotFound)
        {
            return null;
        }

        return new EditorError(EditorErrorCode.Failed, $"The saved sudo password could not be removed: {deleted.Error.Message}");
    }
}
