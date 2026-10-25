using DTech.Core.Credentials;

namespace DTech.Core.Tests.SavedConnections.Support;

internal sealed class InMemoryCredentialStore : ICredentialStore
{
    private readonly Dictionary<string, string> _secrets = [];

    public CredentialError? FailureToReturn { get; set; }

    public IReadOnlyDictionary<string, string> Secrets => _secrets;

    public Task<CredentialResult> Store(CredentialKey key, Secret secret, CancellationToken cancellationToken)
    {
        if (FailureToReturn is not null)
        {
            return Task.FromResult(CredentialResult.Failure(FailureToReturn));
        }

        _secrets[key.Value] = secret.RevealString();
        return Task.FromResult(CredentialResult.Success());
    }

    public Task<CredentialResult<Secret>> Read(CredentialKey key, CancellationToken cancellationToken)
    {
        if (FailureToReturn is not null)
        {
            return Task.FromResult(CredentialResult<Secret>.Failure(FailureToReturn));
        }

        if (!_secrets.TryGetValue(key.Value, out var value))
        {
            var notFound = CredentialErrors.NotFound(key);
            return Task.FromResult(CredentialResult<Secret>.Failure(notFound));
        }

        var secret = Secret.FromString(value);
        return Task.FromResult(CredentialResult<Secret>.Success(secret));
    }

    public Task<CredentialResult> Delete(CredentialKey key, CancellationToken cancellationToken)
    {
        if (FailureToReturn is not null)
        {
            return Task.FromResult(CredentialResult.Failure(FailureToReturn));
        }

        if (!_secrets.Remove(key.Value))
        {
            var notFound = CredentialErrors.NotFound(key);
            return Task.FromResult(CredentialResult.Failure(notFound));
        }

        return Task.FromResult(CredentialResult.Success());
    }
}
