namespace DTech.Core.SettingsTransfer.Import;

public interface ISettingsImportTransaction
{
    Task<SettingsImportResult> RunAsync(Func<CancellationToken, Task<SettingsImportResult>> apply, CancellationToken cancellationToken);
}
