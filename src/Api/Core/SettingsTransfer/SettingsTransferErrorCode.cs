namespace DTech.Core.SettingsTransfer;

public enum SettingsTransferErrorCode
{
    Invalid,
    UnsupportedVersion,
    TooLarge,
    WrongPassword,
    ArchiveDamaged,
    ArchiveMismatch,
    WeakPassword,
    ConfirmationRequired,
    SecretsUnavailable,
}
