using DTech.Core.Workspace;
using DTech.Core.Credentials;

namespace DTech.Core.SettingsTransfer.Import;

public sealed class SettingsImportCoordinator
{
    private readonly ISettingsImportTransaction _transaction;
    private readonly WorkspaceStateWriteCoordinator _workspace;
    private readonly ImportSecretTransaction _secrets;
    private readonly SettingsImportExecutor _executor;
    private readonly CredentialMutationCoordinator _mutations;

    public SettingsImportCoordinator(
        ISettingsImportTransaction transaction,
        WorkspaceStateWriteCoordinator workspace,
        ImportSecretTransaction secrets,
        SettingsImportExecutor executor,
        CredentialMutationCoordinator mutations)
    {
        _transaction = transaction;
        _workspace = workspace;
        _secrets = secrets;
        _executor = executor;
        _mutations = mutations;
    }

    public async Task<SettingsTransferResult<SettingsImportResult>> ExecuteAsync(PreparedSettingsImport prepared, CancellationToken cancellationToken)
    {
        SettingsTransferResult<SettingsImportResult> result = null!;
        await _workspace.RunAsync(async token =>
        {
            using var lease = await _mutations.AcquireAsync(token);
            result = await ApplyAsync(prepared);
        }, cancellationToken);
        return result;
    }

    private async Task<SettingsTransferResult<SettingsImportResult>> ApplyAsync(PreparedSettingsImport prepared)
    {
        SettingsImportResult result;
        try
        {
            var keys = ImportSecretKeys.Collect(prepared);
            await _secrets.CaptureAsync(keys, CancellationToken.None);
            result = await _transaction.RunAsync(token => _executor.ExecuteAsync(prepared, token), CancellationToken.None);
        }
        catch (Exception exception)
        {
            var restored = await _secrets.RollbackAsync(CancellationToken.None);
            var failure = !restored
                ? new SettingsTransferError(SettingsTransferErrorCode.CompensationFailed, "The settings import failed, and some saved passwords could not be restored. Restore those passwords before reconnecting or retrying the import.")
                : exception is SettingsImportException importFailure
                    ? importFailure.Error
                    : new SettingsTransferError(SettingsTransferErrorCode.ImportFailed, "The settings import failed. Saved passwords were restored; check database access before retrying.");
            return SettingsTransferResult<SettingsImportResult>.Failure(failure);
        }
        finally
        {
            _secrets.Dispose();
        }

        result = await _executor.AfterCommitAsync(prepared, result, CancellationToken.None);
        return SettingsTransferResult<SettingsImportResult>.Success(result);
    }
}
