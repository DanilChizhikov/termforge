namespace DTech.Core.SavedConnections;

public enum SavedConnectionErrorCode
{
    Invalid,
    NotFound,
    NameTaken,
    InUse,
    CredentialStoreFailure,
    SshConfigNotFound,
    SshConfigUnreadable,
}
