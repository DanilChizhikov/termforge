using System.Collections.Concurrent;
using DTech.Core.Credentials;

namespace DTech.Entrypoint.Tests.SettingsTransfer.Support;

internal sealed class InMemoryCredentialStore : ICredentialStore
{
    private readonly ConcurrentDictionary<string, string> _secrets = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, string> Secrets => _secrets;

    public CredentialError? FailureToReturn { get; set; }

    public void Put(CredentialKey key, string value)
    {
        _secrets[key.Value] = value;
    }

    public Task<CredentialResult> Store(CredentialKey key, Secret secret, CancellationToken cancellationToken)
    {
        if (FailureToReturn is not null)
        {
            var failure = CredentialResult.Failure(FailureToReturn);
            return Task.FromResult(failure);
        }

        _secrets[key.Value] = secret.RevealString();
        var success = CredentialResult.Success();
        return Task.FromResult(success);
    }

    public Task<CredentialResult<Secret>> Read(CredentialKey key, CancellationToken cancellationToken)
    {
        if (!_secrets.TryGetValue(key.Value, out var value))
        {
            var notFound = CredentialErrors.NotFound(key);
            var missing = CredentialResult<Secret>.Failure(notFound);
            return Task.FromResult(missing);
        }

        var secret = Secret.FromString(value);
        var found = CredentialResult<Secret>.Success(secret);
        return Task.FromResult(found);
    }

    public Task<CredentialResult> Delete(CredentialKey key, CancellationToken cancellationToken)
    {
        if (!_secrets.TryRemove(key.Value, out _))
        {
            var notFound = CredentialErrors.NotFound(key);
            var missing = CredentialResult.Failure(notFound);
            return Task.FromResult(missing);
        }

        var success = CredentialResult.Success();
        return Task.FromResult(success);
    }
}
