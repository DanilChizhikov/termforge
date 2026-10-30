namespace DTech.Core.SettingsTransfer;

public enum SettingsTransferErrorCode
{
    Invalid,
    UnsupportedVersion,
    TooLarge,
    WrongKey,
    FileDamaged,
    ConfirmationRequired,
    SecretsUnavailable,
}
