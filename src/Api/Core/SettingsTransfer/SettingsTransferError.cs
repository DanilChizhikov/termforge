namespace DTech.Core.SettingsTransfer;

public sealed record SettingsTransferError(SettingsTransferErrorCode Code, string Message)
{
    public static SettingsTransferError Invalid(string message)
    {
        return new SettingsTransferError(SettingsTransferErrorCode.Invalid, message);
    }

    public static SettingsTransferError ArchiveDamaged()
    {
        return new SettingsTransferError(SettingsTransferErrorCode.ArchiveDamaged, "The secrets archive is damaged or was modified after it was exported.");
    }

    public static SettingsTransferError WrongPassword()
    {
        return new SettingsTransferError(SettingsTransferErrorCode.WrongPassword, "The archive password is incorrect.");
    }

    public static SettingsTransferError ArchiveMismatch()
    {
        return new SettingsTransferError(SettingsTransferErrorCode.ArchiveMismatch, "The secrets archive belongs to a different settings export. Choose the archive exported together with this settings file.");
    }

    public static SettingsTransferError TooLarge(string what, int limit)
    {
        return new SettingsTransferError(SettingsTransferErrorCode.TooLarge, $"The {what} is larger than {limit} bytes.");
    }
}
