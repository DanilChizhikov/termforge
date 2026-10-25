using System.Security.Cryptography;
using System.Text;
using DTech.Core.Credentials;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DTech.Credentials.Vault;

public sealed class EncryptedVault : ICredentialStore, IDisposable
{
    public const int MinimumMasterPasswordLength = 8;

    private readonly VaultFile _file;
    private readonly IOptions<CredentialStoreOptions> _options;
    private readonly ILogger<EncryptedVault> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private VaultKeys? _keys;

    public EncryptedVault(VaultFile file, IOptions<CredentialStoreOptions> options, ILogger<EncryptedVault> logger)
    {
        _file = file;
        _options = options;
        _logger = logger;
    }

    public string FilePath => _file.FilePath;

    public CredentialVaultState State
    {
        get
        {
            if (_keys is not null)
            {
                return CredentialVaultState.Unlocked;
            }

            return _file.Exists ? CredentialVaultState.Locked : CredentialVaultState.NotCreated;
        }
    }

    public async Task<CredentialResult> Unlock(Secret masterPassword, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var unlockResult = await UnlockOrCreate(masterPassword, cancellationToken);
            if (!unlockResult.IsSuccess)
            {
                _logger.LogWarning($"Encrypted vault unlock failed: {unlockResult.Error.Code}");
                return CredentialResult.Failure(unlockResult.Error);
            }

            _keys?.Dispose();
            _keys = unlockResult.Value;
            _logger.LogInformation("Encrypted vault unlocked");
            return CredentialResult.Success();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return FileAccessFailure(exception);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Lock()
    {
        _gate.Wait();
        try
        {
            _keys?.Dispose();
            _keys = null;
            _logger.LogInformation("Encrypted vault locked");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<CredentialResult> Store(CredentialKey key, Secret secret, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_keys is null)
            {
                var lockedError = CredentialErrors.VaultLocked();
                return CredentialResult.Failure(lockedError);
            }

            var openResult = await OpenEntries(_keys, cancellationToken);
            if (!openResult.IsSuccess)
            {
                return CredentialResult.Failure(openResult.Error);
            }

            using var entries = openResult.Value;
            var secretBytes = secret.AsSpan();
            entries.Set(key.Value, secretBytes);
            await Save(_keys, entries, cancellationToken);
            return CredentialResult.Success();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return FileAccessFailure(exception);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<CredentialResult<Secret>> Read(CredentialKey key, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_keys is null)
            {
                var lockedError = CredentialErrors.VaultLocked();
                return CredentialResult<Secret>.Failure(lockedError);
            }

            var openResult = await OpenEntries(_keys, cancellationToken);
            if (!openResult.IsSuccess)
            {
                return CredentialResult<Secret>.Failure(openResult.Error);
            }

            using var entries = openResult.Value;
            var entry = entries.Find(key.Value);
            if (entry is null)
            {
                var notFoundError = CredentialErrors.NotFound(key);
                return CredentialResult<Secret>.Failure(notFoundError);
            }

            var secret = Secret.FromBytes(entry.Value);
            return CredentialResult<Secret>.Success(secret);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            var error = CreateFileAccessError(exception);
            return CredentialResult<Secret>.Failure(error);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<CredentialResult> Delete(CredentialKey key, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_keys is null)
            {
                var lockedError = CredentialErrors.VaultLocked();
                return CredentialResult.Failure(lockedError);
            }

            var openResult = await OpenEntries(_keys, cancellationToken);
            if (!openResult.IsSuccess)
            {
                return CredentialResult.Failure(openResult.Error);
            }

            using var entries = openResult.Value;
            if (!entries.Remove(key.Value))
            {
                var notFoundError = CredentialErrors.NotFound(key);
                return CredentialResult.Failure(notFoundError);
            }

            await Save(_keys, entries, cancellationToken);
            return CredentialResult.Success();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return FileAccessFailure(exception);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        _keys?.Dispose();
        _keys = null;
        _gate.Dispose();
    }

    private async Task<CredentialResult<VaultKeys>> UnlockOrCreate(Secret masterPassword, CancellationToken cancellationToken)
    {
        var file = await _file.Read(cancellationToken);
        if (file is null)
        {
            return await Create(masterPassword, cancellationToken);
        }

        var headerResult = VaultFileFormat.ReadHeader(file);
        if (!headerResult.IsSuccess)
        {
            return CredentialResult<VaultKeys>.Failure(headerResult.Error);
        }

        var header = headerResult.Value;
        var passwordBytes = masterPassword.AsSpan();
        var keys = VaultKeyDerivation.Derive(passwordBytes, header.KdfParameters);
        if (!VaultCipher.IsPasswordCorrect(keys, header))
        {
            keys.Dispose();
            var passwordError = CredentialErrors.InvalidMasterPassword();
            return CredentialResult<VaultKeys>.Failure(passwordError);
        }

        var decryptResult = VaultCipher.Decrypt(keys, header, file);
        if (!decryptResult.IsSuccess)
        {
            keys.Dispose();
            return CredentialResult<VaultKeys>.Failure(decryptResult.Error);
        }

        var parseResult = VaultEntriesSerializer.Deserialize(decryptResult.Value);
        CryptographicOperations.ZeroMemory(decryptResult.Value);
        if (!parseResult.IsSuccess)
        {
            keys.Dispose();
            return CredentialResult<VaultKeys>.Failure(parseResult.Error);
        }

        parseResult.Value.Dispose();
        return CredentialResult<VaultKeys>.Success(keys);
    }

    private async Task<CredentialResult<VaultKeys>> Create(Secret masterPassword, CancellationToken cancellationToken)
    {
        var passwordBytes = masterPassword.AsSpan();
        var passwordLength = Encoding.UTF8.GetCharCount(passwordBytes);
        if (passwordLength < MinimumMasterPasswordLength)
        {
            var weakError = CredentialErrors.WeakMasterPassword(MinimumMasterPasswordLength);
            return CredentialResult<VaultKeys>.Failure(weakError);
        }

        var kdfParameters = VaultKeyDerivation.CreateParameters(_options.Value.VaultKdfIterations);
        var keys = VaultKeyDerivation.Derive(passwordBytes, kdfParameters);
        using var entries = new VaultEntries();
        await Save(keys, entries, cancellationToken);
        _logger.LogInformation($"Encrypted vault created at {_file.FilePath}");
        return CredentialResult<VaultKeys>.Success(keys);
    }

    private async Task<CredentialResult<VaultEntries>> OpenEntries(VaultKeys keys, CancellationToken cancellationToken)
    {
        var file = await _file.Read(cancellationToken);
        if (file is null)
        {
            var missingError = CredentialErrors.VaultCorrupted($"the vault file '{_file.FilePath}' no longer exists.");
            return CredentialResult<VaultEntries>.Failure(missingError);
        }

        var headerResult = VaultFileFormat.ReadHeader(file);
        if (!headerResult.IsSuccess)
        {
            return CredentialResult<VaultEntries>.Failure(headerResult.Error);
        }

        var header = headerResult.Value;
        if (!keys.Matches(header.KdfParameters) || !VaultCipher.IsPasswordCorrect(keys, header))
        {
            var replacedError = CredentialErrors.VaultCorrupted("the vault file was replaced after it was unlocked. Lock and unlock the vault again.");
            return CredentialResult<VaultEntries>.Failure(replacedError);
        }

        var decryptResult = VaultCipher.Decrypt(keys, header, file);
        if (!decryptResult.IsSuccess)
        {
            return CredentialResult<VaultEntries>.Failure(decryptResult.Error);
        }

        var parseResult = VaultEntriesSerializer.Deserialize(decryptResult.Value);
        CryptographicOperations.ZeroMemory(decryptResult.Value);
        return parseResult;
    }

    private async Task Save(VaultKeys keys, VaultEntries entries, CancellationToken cancellationToken)
    {
        var plaintext = VaultEntriesSerializer.Serialize(entries);
        try
        {
            var file = VaultCipher.Encrypt(keys, plaintext);
            await _file.Write(file, cancellationToken);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private CredentialResult FileAccessFailure(Exception exception)
    {
        var error = CreateFileAccessError(exception);
        return CredentialResult.Failure(error);
    }

    private CredentialError CreateFileAccessError(Exception exception)
    {
        _logger.LogError($"Encrypted vault file access failed: {exception.GetType().Name}: {exception.Message}");
        return CredentialErrors.StoreFailure($"The encrypted vault file '{_file.FilePath}' could not be accessed: {exception.Message}");
    }
}
