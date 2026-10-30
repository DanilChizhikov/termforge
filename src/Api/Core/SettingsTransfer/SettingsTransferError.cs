namespace DTech.Core.SettingsTransfer;

public sealed record SettingsTransferError(SettingsTransferErrorCode Code, string Message)
{
    public static SettingsTransferError Invalid(string message)
    {
        return new SettingsTransferError(SettingsTransferErrorCode.Invalid, message);
    }

    public static SettingsTransferError FileDamaged()
    {
        return new SettingsTransferError(SettingsTransferErrorCode.FileDamaged, "The settings file is damaged, was modified after it was exported, or is not a TermForge settings export.");
    }

    public static SettingsTransferError WrongKey()
    {
        return new SettingsTransferError(SettingsTransferErrorCode.WrongKey, "The key is incorrect.");
    }

    public static SettingsTransferError TooLarge(string what, int limit)
    {
        return new SettingsTransferError(SettingsTransferErrorCode.TooLarge, $"The {what} is larger than {limit} bytes.");
    }
}
