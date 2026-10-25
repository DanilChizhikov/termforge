namespace DTech.Core.Credentials;

public enum CredentialErrorCode
{
    InvalidKey,
    InvalidSecret,
    NotFound,
    StoreUnavailable,
    StoreFailure,
    VaultLocked,
    VaultNotInUse,
    InvalidMasterPassword,
    WeakMasterPassword,
    VaultCorrupted,
    UnsupportedVaultVersion,
}
