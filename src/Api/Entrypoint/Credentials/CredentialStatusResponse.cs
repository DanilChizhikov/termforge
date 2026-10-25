namespace DTech.Entrypoint.Credentials;

public sealed record CredentialStatusResponse(
    string Backend,
    string SelectionReason,
    OperatingSystemStoreStatusResponse OperatingSystemStore,
    string VaultState);
