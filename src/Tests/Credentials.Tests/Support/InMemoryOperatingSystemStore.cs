using System.Collections.Concurrent;
using DTech.Core.Credentials;
using DTech.Credentials.Native;

namespace DTech.Credentials.Tests.Support;

public sealed class InMemoryOperatingSystemStore : IOperatingSystemCredentialStore
{
    private readonly ConcurrentDictionary<string, byte[]> _items = new(StringComparer.Ordinal);

    public string Name => "In-memory test store";

    public int StoreCallCount { get; private set; }

    public byte[] LastStoredValue { get; private set; } = [];

    public IReadOnlyCollection<string> Keys => _items.Keys.ToArray();

    public CredentialStoreAvailability CheckPrerequisites()
    {
        return CredentialStoreAvailability.Available(Name);
    }

    public Task<CredentialResult> Store(CredentialKey key, Secret secret, CancellationToken cancellationToken)
    {
        StoreCallCount++;
        var value = secret.AsSpan().ToArray();
        _items[key.Value] = value;
        LastStoredValue = value.ToArray();
        var result = CredentialResult.Success();
        return Task.FromResult(result);
    }

    public Task<CredentialResult<Secret>> Read(CredentialKey key, CancellationToken cancellationToken)
    {
        if (!_items.TryGetValue(key.Value, out var value))
        {
            var error = CredentialErrors.NotFound(key);
            var failure = CredentialResult<Secret>.Failure(error);
            return Task.FromResult(failure);
        }

        var secret = Secret.FromBytes(value);
        var result = CredentialResult<Secret>.Success(secret);
        return Task.FromResult(result);
    }

    public Task<CredentialResult> Delete(CredentialKey key, CancellationToken cancellationToken)
    {
        if (!_items.TryRemove(key.Value, out _))
        {
            var error = CredentialErrors.NotFound(key);
            var failure = CredentialResult.Failure(error);
            return Task.FromResult(failure);
        }

        var result = CredentialResult.Success();
        return Task.FromResult(result);
    }
}
