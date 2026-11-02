namespace DTech.Core.SettingsTransfer.Import;

public sealed class SettingsImportException(SettingsTransferError error) : Exception(error.Message)
{
    public SettingsTransferError Error { get; } = error;
}
