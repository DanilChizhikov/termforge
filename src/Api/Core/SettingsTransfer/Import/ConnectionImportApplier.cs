using DTech.Core.SavedConnections;
using DTech.Core.SettingsTransfer.Secrets;

namespace DTech.Core.SettingsTransfer.Import;

public sealed class ConnectionImportApplier(ISavedConnectionRepository repository, ConnectionSecretWriter secretWriter, TimeProvider timeProvider)
{
    public async Task<ConnectionImportOutcome> ApplyAsync(SettingsImportPlan plan, SecretsArchiveContent? secrets, CancellationToken cancellationToken)
    {
        var outcome = new ConnectionImportOutcome();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var context = new ConnectionImportContext(secrets, outcome, now);
        var writes = ConnectionWriteOrder.JumpHostsFirst(plan.Steps);
        for (var i = 0; i < writes.Count; i++)
        {
            await WriteAsync(writes[i], context, cancellationToken);
        }

        for (var i = 0; i < plan.Steps.Count; i++)
        {
            var step = plan.Steps[i];
            if (step.Action == ConnectionImportAction.Unchanged)
            {
                outcome.Unchanged++;
                await ApplySecretsToUnchangedAsync(step, context, cancellationToken);
            }
            else if (step.Action == ConnectionImportAction.Conflict && step.Resolution == ConnectionConflictResolution.KeepExisting)
            {
                outcome.Kept++;
            }
        }

        var removals = ConnectionWriteOrder.DependentsFirst(plan.Removals);
        for (var i = 0; i < removals.Count; i++)
        {
            await RemoveAsync(removals[i], context, cancellationToken);
        }

        return outcome;
    }

    private async Task WriteAsync(ConnectionImportStep step, ConnectionImportContext context, CancellationToken cancellationToken)
    {
        var hasPassword = await ApplyPasswordAsync(step, context, cancellationToken);
        await StoreSudoPasswordAsync(step, context, cancellationToken);
        var existing = step.IsNewConnection ? null : step.Existing;
        if (existing is null)
        {
            var connection = new SavedConnection
            {
                Id = step.LocalId,
                Details = step.Draft,
                HasPassword = hasPassword,
                CreatedAt = context.Now,
                UpdatedAt = context.Now,
            };
            await repository.AddAsync(connection, cancellationToken);
            context.Outcome.Added++;
            return;
        }

        var updated = existing with { Details = step.Draft, HasPassword = hasPassword, UpdatedAt = context.Now };
        await repository.UpdateAsync(updated, cancellationToken);
        context.Outcome.Updated++;
    }

    private async Task<bool> ApplyPasswordAsync(ConnectionImportStep step, ConnectionImportContext context, CancellationToken cancellationToken)
    {
        var existing = step.IsNewConnection ? null : step.Existing;
        var hadPassword = existing is not null && existing.HasPassword;
        if (step.Draft.AuthMethod != ConnectionAuthMethod.Password)
        {
            if (hadPassword)
            {
                var removeError = await secretWriter.RemoveAsync(step.LocalId, ArchivedSecretKind.Password, cancellationToken);
                AddWarningIf(context, removeError is not null, $"'{step.Draft.Name}': the old saved password could not be removed: {removeError?.Message}");
            }

            return false;
        }

        var password = context.FindSecret(step, ArchivedSecretKind.Password);
        if (password is null)
        {
            return hadPassword;
        }

        var storeError = await secretWriter.StoreAsync(password, step.LocalId, cancellationToken);
        if (storeError is not null)
        {
            context.Outcome.Warnings.Add($"'{step.Draft.Name}': the password could not be saved ({storeError.Message}). Enter it again in the server settings.");
            return hadPassword;
        }

        context.Outcome.SecretsStored++;
        return true;
    }

    private async Task StoreSudoPasswordAsync(ConnectionImportStep step, ConnectionImportContext context, CancellationToken cancellationToken)
    {
        var sudoPassword = context.FindSecret(step, ArchivedSecretKind.SudoPassword);
        if (sudoPassword is null)
        {
            return;
        }

        var storeError = await secretWriter.StoreAsync(sudoPassword, step.LocalId, cancellationToken);
        if (storeError is not null)
        {
            context.Outcome.Warnings.Add($"'{step.Draft.Name}': the sudo password could not be saved ({storeError.Message}).");
            return;
        }

        context.Outcome.SecretsStored++;
    }

    private async Task ApplySecretsToUnchangedAsync(ConnectionImportStep step, ConnectionImportContext context, CancellationToken cancellationToken)
    {
        await StoreSudoPasswordAsync(step, context, cancellationToken);
        var existing = step.Existing!;
        var password = context.FindSecret(step, ArchivedSecretKind.Password);
        if (password is null || existing.Details.AuthMethod != ConnectionAuthMethod.Password)
        {
            return;
        }

        var storeError = await secretWriter.StoreAsync(password, existing.Id, cancellationToken);
        if (storeError is not null)
        {
            context.Outcome.Warnings.Add($"'{existing.Details.Name}': the password could not be saved ({storeError.Message}).");
            return;
        }

        context.Outcome.SecretsStored++;
        if (!existing.HasPassword)
        {
            var updated = existing with { HasPassword = true, UpdatedAt = context.Now };
            await repository.UpdateAsync(updated, cancellationToken);
        }
    }

    private async Task RemoveAsync(SavedConnection connection, ConnectionImportContext context, CancellationToken cancellationToken)
    {
        var passwordError = await secretWriter.RemoveAsync(connection.Id, ArchivedSecretKind.Password, cancellationToken);
        var sudoError = await secretWriter.RemoveAsync(connection.Id, ArchivedSecretKind.SudoPassword, cancellationToken);
        var failure = passwordError ?? sudoError;
        AddWarningIf(context, failure is not null, $"'{connection.Details.Name}': its saved passwords could not be removed: {failure?.Message}");
        await repository.DeleteAsync(connection.Id, cancellationToken);
        context.Outcome.Removed++;
    }

    private static void AddWarningIf(ConnectionImportContext context, bool condition, string warning)
    {
        if (condition)
        {
            context.Outcome.Warnings.Add(warning);
        }
    }
}
