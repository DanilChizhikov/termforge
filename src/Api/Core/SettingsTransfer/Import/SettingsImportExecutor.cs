namespace DTech.Core.SettingsTransfer.Import;

public sealed class SettingsImportExecutor
{
    private readonly ConnectionImportApplier _connections;
    private readonly PreferencesImportApplier _preferences;
    private readonly WorkspaceImportApplier _workspace;
    private readonly PinnedCommandImportApplier _pinnedCommands;

    public SettingsImportExecutor(
        ConnectionImportApplier connections,
        PreferencesImportApplier preferences,
        WorkspaceImportApplier workspace,
        PinnedCommandImportApplier pinnedCommands)
    {
        _connections = connections;
        _preferences = preferences;
        _workspace = workspace;
        _pinnedCommands = pinnedCommands;
    }

    public async Task<SettingsImportResult> ExecuteAsync(PreparedSettingsImport prepared, CancellationToken cancellationToken)
    {
        var document = prepared.Loaded.Document;
        var sections = prepared.Sections;
        var plan = prepared.Plan;
        var outcome = new ConnectionImportOutcome();
        if (sections.Connections)
        {
            outcome = await _connections.ApplyAsync(plan, prepared.Loaded.Secrets, cancellationToken);
        }

        var warnings = new List<string>(outcome.Warnings);
        var preferencesImported = false;
        if (prepared.Preferences is not null)
        {
            var preferencesError = await _preferences.ApplyAsync(prepared.Preferences, cancellationToken);
            preferencesImported = preferencesError is null;
            ThrowIfPresent(preferencesError);
        }

        var workspaceImported = false;
        if (sections.Workspace)
        {
            var workspaceError = await _workspace.ApplyAsync(document.Workspace!, plan, cancellationToken);
            workspaceImported = workspaceError is null;
            ThrowIfPresent(workspaceError);
        }

        var pinned = new PinnedCommandImportOutcome(0, 0);
        if (sections.PinnedCommands)
        {
            pinned = await _pinnedCommands.ApplyAsync(document.PinnedCommands!, plan, cancellationToken);
        }

        return new SettingsImportResult
        {
            Added = outcome.Added,
            Updated = outcome.Updated,
            Unchanged = outcome.Unchanged,
            Kept = outcome.Kept,
            Removed = outcome.Removed,
            SecretsStored = outcome.SecretsStored,
            PreferencesImported = preferencesImported,
            WorkspaceImported = workspaceImported,
            PinnedCommandsAdded = pinned.Added,
            PinnedCommandsSkipped = pinned.Skipped,
            Warnings = warnings,
        };
    }

    public async Task<SettingsImportResult> AfterCommitAsync(PreparedSettingsImport prepared, SettingsImportResult result, CancellationToken cancellationToken)
    {
        if (prepared.Preferences is null)
        {
            return result;
        }

        try
        {
            await _preferences.AfterCommitAsync(prepared.Preferences, cancellationToken);
            return result;
        }
        catch (Exception)
        {
            var warnings = result.Warnings.ToList();
            warnings.Add("Settings were imported, but terminal recording cleanup could not finish. Save recording settings or restart the application to retry cleanup.");
            return result with { Warnings = warnings };
        }
    }

    private static void ThrowIfPresent(string? warning)
    {
        if (warning is not null)
        {
            var error = new SettingsTransferError(SettingsTransferErrorCode.ImportFailed, warning);
            throw new SettingsImportException(error);
        }
    }
}
