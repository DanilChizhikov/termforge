using DTech.Core.Credentials;

namespace DTech.Core.Editor.Sudo;

public sealed class SudoPasswordResolver(SudoPasswordStore store)
{
    private static readonly char[] ForbiddenCharacters = ['\n', '\r', '\0'];

    public async Task<EditorResult<Secret?>> ResolveAsync(Guid connectionId, SudoCredentials credentials, CancellationToken cancellationToken)
    {
        var password = credentials.Password;
        if (password is null)
        {
            return await store.ReadAsync(connectionId, cancellationToken);
        }

        if (password.IndexOfAny(ForbiddenCharacters) >= 0)
        {
            var invalid = EditorError.Invalid(SudoErrors.InvalidPasswordMessage);
            return EditorResult<Secret?>.Failure(invalid);
        }

        var secret = Secret.FromString(password);
        return EditorResult<Secret?>.Success(secret);
    }
}
