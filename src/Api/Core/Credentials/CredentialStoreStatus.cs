namespace DTech.Core.Credentials;

public sealed record CredentialStoreStatus(
    CredentialBackend Backend,
    string SelectionReason,
    CredentialStoreAvailability OperatingSystemStore,
    CredentialVaultState VaultState);
