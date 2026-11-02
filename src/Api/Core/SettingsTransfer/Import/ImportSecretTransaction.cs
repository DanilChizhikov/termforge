using DTech.Core.Credentials;

namespace DTech.Core.SettingsTransfer.Import;

public sealed class ImportSecretTransaction(IUncoordinatedCredentialStore store) : ICredentialStore, IDisposable
{
    private readonly Dictionary<CredentialKey, Secret?> _snapshots = [];
    private readonly List<CredentialKey> _attempted = [];
    private readonly HashSet<CredentialKey> _attemptedKeys = [];

    public async Task CaptureAsync(IReadOnlyList<CredentialKey> keys, CancellationToken cancellationToken)
    {
        for (var i = 0; i < keys.Count; i++)
        {
            var key = keys[i];
            if (_snapshots.ContainsKey(key))
            {
                continue;
            }

            var read = await store.Read(key, cancellationToken);
            if (read.IsSuccess)
            {
                _snapshots.Add(key, read.Value);
            }
            else if (read.Error.Code == CredentialErrorCode.NotFound)
            {
                _snapshots.Add(key, null);
            }
            else
            {
                throw Failure("read", read.Error);
            }
        }
    }

    public async Task<CredentialResult> Store(CredentialKey key, Secret secret, CancellationToken cancellationToken)
    {
        TrackAttempt(key);
        var result = await store.Store(key, secret, cancellationToken);
        if (!result.IsSuccess)
        {
            throw Failure("save", result.Error);
        }

        return result;
    }

    public Task<CredentialResult<Secret>> Read(CredentialKey key, CancellationToken cancellationToken)
    {
        return store.Read(key, cancellationToken);
    }

    public async Task<CredentialResult> Delete(CredentialKey key, CancellationToken cancellationToken)
    {
        TrackAttempt(key);
        var result = await store.Delete(key, cancellationToken);
        if (!result.IsSuccess && result.Error.Code != CredentialErrorCode.NotFound)
        {
            throw Failure("remove", result.Error);
        }

        return result;
    }

    public async Task<bool> RollbackAsync(CancellationToken cancellationToken)
    {
        var restored = true;
        for (var i = _attempted.Count - 1; i >= 0; i--)
        {
            var key = _attempted[i];
            try
            {
                var original = _snapshots[key];
                var result = original is null
                    ? await store.Delete(key, cancellationToken)
                    : await store.Store(key, original, cancellationToken);
                if (!result.IsSuccess && !(original is null && result.Error.Code == CredentialErrorCode.NotFound))
                {
                    restored = false;
                }
            }
            catch (Exception)
            {
                restored = false;
            }
        }

        return restored;
    }

    public void Dispose()
    {
        foreach (var secret in _snapshots.Values)
        {
            secret?.Dispose();
        }

        _snapshots.Clear();
        _attempted.Clear();
        _attemptedKeys.Clear();
    }

    private void TrackAttempt(CredentialKey key)
    {
        if (!_snapshots.ContainsKey(key))
        {
            throw new InvalidOperationException("A settings import cannot mutate a password without a snapshot.");
        }

        if (_attemptedKeys.Add(key))
        {
            _attempted.Add(key);
        }
    }

    private static SettingsImportException Failure(string operation, CredentialError error)
    {
        var failure = new SettingsTransferError(SettingsTransferErrorCode.SecretsUnavailable, $"A saved password could not be readied for import ({operation}: {error.Code}).");
        return new SettingsImportException(failure);
    }
}
