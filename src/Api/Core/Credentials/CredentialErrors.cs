namespace DTech.Core.Credentials;

public static class CredentialErrors
{
    public static CredentialError InvalidKey(string reason)
    {
        return new CredentialError(CredentialErrorCode.InvalidKey, $"Invalid credential key: {reason}");
    }

    public static CredentialError InvalidSecret(string reason)
    {
        return new CredentialError(CredentialErrorCode.InvalidSecret, $"Invalid secret: {reason}");
    }

    public static CredentialError NotFound(CredentialKey key)
    {
        return new CredentialError(CredentialErrorCode.NotFound, $"Credential '{key.Value}' was not found.");
    }

    public static CredentialError StoreUnavailable(string diagnostic)
    {
        return new CredentialError(CredentialErrorCode.StoreUnavailable, diagnostic);
    }

    public static CredentialError StoreFailure(string detail)
    {
        return new CredentialError(CredentialErrorCode.StoreFailure, detail);
    }

    public static CredentialError VaultLocked()
    {
        return new CredentialError(CredentialErrorCode.VaultLocked, "The encrypted credential vault is locked. Unlock it with the master password.");
    }

    public static CredentialError VaultNotInUse()
    {
        return new CredentialError(CredentialErrorCode.VaultNotInUse, "The encrypted credential vault is not in use because the operating system credential store is available.");
    }

    public static CredentialError InvalidMasterPassword()
    {
        return new CredentialError(CredentialErrorCode.InvalidMasterPassword, "The master password is incorrect.");
    }

    public static CredentialError WeakMasterPassword(int minimumLength)
    {
        return new CredentialError(CredentialErrorCode.WeakMasterPassword, $"The master password must be at least {minimumLength} characters long.");
    }

    public static CredentialError VaultCorrupted(string detail)
    {
        return new CredentialError(CredentialErrorCode.VaultCorrupted, $"The encrypted credential vault is damaged or was modified outside the application: {detail}");
    }

    public static CredentialError UnsupportedVaultVersion(int version)
    {
        return new CredentialError(CredentialErrorCode.UnsupportedVaultVersion, $"The encrypted credential vault uses format version {version}, which this version of the application cannot read. Update the application.");
    }
}
