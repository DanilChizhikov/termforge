using DTech.Core.Credentials;
using DTech.Credentials.Native;
using DTech.Credentials.Vault;
using Microsoft.Extensions.Logging;

namespace DTech.Credentials.Selection;

public sealed class CredentialBackendSelector
{
    private readonly IOperatingSystemCredentialStore _operatingSystemStore;
    private readonly OperatingSystemCredentialStoreProbe _probe;
    private readonly EncryptedVault _vault;
    private readonly ILogger<CredentialBackendSelector> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CredentialBackendSelection? _selection;

    public CredentialBackendSelector(
        IOperatingSystemCredentialStore operatingSystemStore,
        OperatingSystemCredentialStoreProbe probe,
        EncryptedVault vault,
        ILogger<CredentialBackendSelector> logger)
    {
        _operatingSystemStore = operatingSystemStore;
        _probe = probe;
        _vault = vault;
        _logger = logger;
    }

    public async Task<CredentialBackendSelection> Select(CancellationToken cancellationToken)
    {
        if (_selection is not null)
        {
            return _selection;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            _selection ??= await SelectBackend(cancellationToken);
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
            var existingVaultReason = $"The encrypted vault at '{_vault.FilePath}' already exists, so it stays in use to keep its secrets reachable. {availability.Diagnostic}";
            _logger.LogInformation($"Credential backend: encrypted vault. {existingVaultReason}");
            return new CredentialBackendSelection(CredentialBackend.EncryptedVault, _vault, existingVaultReason, availability);
        }

        if (availability.IsAvailable)
        {
            var availableReason = $"{availability.StoreName} passed the availability self-test.";
            _logger.LogInformation($"Credential backend: {availability.StoreName}");
            return new CredentialBackendSelection(CredentialBackend.OperatingSystemStore, _operatingSystemStore, availableReason, availability);
        }

        var fallbackReason = $"{availability.Diagnostic} Secrets are stored in the encrypted vault at '{_vault.FilePath}' instead, which must be unlocked with a master password.";
        _logger.LogWarning($"Operating system credential store is unavailable, falling back to the encrypted vault. {fallbackReason}");
        return new CredentialBackendSelection(CredentialBackend.EncryptedVault, _vault, fallbackReason, availability);
    }
}
