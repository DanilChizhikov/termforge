namespace DTech.Core.SettingsTransfer.Import;

public sealed class SettingsImportService(SettingsImportPreparer preparer, SettingsImportExecutor executor)
{
    public async Task<SettingsTransferResult<SettingsImportPreview>> PreviewAsync(SettingsImportRequest request, CancellationToken cancellationToken)
    {
        var prepared = await preparer.PrepareAsync(request, cancellationToken);
        if (!prepared.IsSuccess)
        {
            return SettingsTransferResult<SettingsImportPreview>.Failure(prepared.Error);
        }

        using var import = prepared.Value;
        var preview = SettingsImportPreviewBuilder.Build(import);
        return SettingsTransferResult<SettingsImportPreview>.Success(preview);
    }

    public async Task<SettingsTransferResult<SettingsImportResult>> ApplyAsync(SettingsImportRequest request, CancellationToken cancellationToken)
    {
        var prepared = await preparer.PrepareAsync(request, cancellationToken);
        if (!prepared.IsSuccess)
        {
            return SettingsTransferResult<SettingsImportResult>.Failure(prepared.Error);
        }

        using var import = prepared.Value;
        var removals = import.Plan.Removals.Count;
        if (removals > 0 && !request.Options.ConfirmReplace)
        {
            var confirmation = new SettingsTransferError(
                SettingsTransferErrorCode.ConfirmationRequired,
                $"Replacing removes {removals} saved servers that are not in the settings file. Confirm the replacement to continue.");
            return SettingsTransferResult<SettingsImportResult>.Failure(confirmation);
        }

        var result = await executor.ExecuteAsync(import, cancellationToken);
        return SettingsTransferResult<SettingsImportResult>.Success(result);
    }
}
