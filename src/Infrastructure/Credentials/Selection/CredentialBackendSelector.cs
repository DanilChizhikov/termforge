using DTech.Core.Credentials;
using DTech.Credentials.Native;
using DTech.Credentials.Vault;
using Microsoft.Extensions.Logging;

namespace DTech.Credentials.Selection;

public sealed class CredentialBackendSelector
{
    public static readonly TimeSpan FailedProbeRetryDelay = TimeSpan.FromSeconds(5);

    private readonly IOperatingSystemCredentialStore _operatingSystemStore;
    private readonly OperatingSystemCredentialStoreProbe _probe;
    private readonly EncryptedVault _vault;
    private readonly ILogger<CredentialBackendSelector> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CredentialBackendSelection? _selection;
    private readonly TimeProvider _timeProvider;
    private DateTimeOffset _retryAfter;

    public CredentialBackendSelector(
        IOperatingSystemCredentialStore operatingSystemStore,
        OperatingSystemCredentialStoreProbe probe,
        EncryptedVault vault,
        ILogger<CredentialBackendSelector> logger,
        TimeProvider? timeProvider = null)
    {
        _operatingSystemStore = operatingSystemStore;
        _probe = probe;
        _vault = vault;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<CredentialBackendSelection> Select(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var vaultExists = _vault.State != CredentialVaultState.NotCreated;
            var now = _timeProvider.GetUtcNow();
            if (_selection is not null &&
                ((_selection.Backend == CredentialBackend.EncryptedVault && vaultExists) ||
                 (_selection.Backend == CredentialBackend.OperatingSystemStore && !vaultExists) ||
                 (!vaultExists && now < _retryAfter)))
            {
                return _selection;
            }

            _selection = await SelectBackend(cancellationToken);
            _retryAfter = _timeProvider.GetUtcNow() + FailedProbeRetryDelay;
            return _selection;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<CredentialBackendSelection> SelectBackend(CancellationToken cancellationToken)
    {
        var availability = await _probe.Probe(cancellationToken);
        if (_vault.State != CredentialVaultState.NotCreated)
        {
            var existingVaultReason = $"The encrypted vault at '{_vault.FilePath}' already exists, so it stays in use to keep its secrets reachable. Operating system secrets have not been migrated to this vault. {availability.Diagnostic}";
            _logger.LogInformation($"Credential backend: encrypted vault. {existingVaultReason}");
            return new CredentialBackendSelection(CredentialBackend.EncryptedVault, _vault, existingVaultReason, availability);
        }

        if (availability.IsAvailable)
        {
            var availableReason = $"{availability.StoreName} passed the availability self-test.";
            _logger.LogInformation($"Credential backend: {availability.StoreName}");
            return new CredentialBackendSelection(CredentialBackend.OperatingSystemStore, _operatingSystemStore, availableReason, availability);
        }

        var fallbackReason = $"{availability.Diagnostic} The encrypted vault at '{_vault.FilePath}' must be unlocked with a master password before it can store secrets. Operating system secrets have not been migrated and remain unavailable through the vault. The operating system store will be checked again before a vault is created.";
        _logger.LogWarning($"Operating system credential store is unavailable, falling back to the encrypted vault. {fallbackReason}");
        return new CredentialBackendSelection(CredentialBackend.EncryptedVault, _vault, fallbackReason, availability);
    }
}
