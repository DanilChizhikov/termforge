using DTech.Core.Credentials;

namespace DTech.Credentials.Selection;

public sealed record CredentialBackendSelection(
    CredentialBackend Backend,
    ICredentialStore Store,
    string Reason,
    CredentialStoreAvailability OperatingSystemStore);
