using DTech.Core.SavedConnections;
using DTech.Core.SettingsTransfer.Secrets;

namespace DTech.Core.SettingsTransfer.Import;

public static class SettingsImportPreviewBuilder
{
    public static SettingsImportPreview Build(PreparedSettingsImport prepared)
    {
        var document = prepared.Loaded.Document;
        var sections = prepared.Sections;
        var plan = prepared.Plan;
        var warnings = CollectWarnings(prepared);
        WorkspaceImportSummary? workspace = null;
        if (sections.Workspace)
        {
            var remapped = WorkspaceIdRemapper.Remap(document.Workspace!, plan);
            workspace = new WorkspaceImportSummary(remapped.TerminalTabs.Count, remapped.EditorTabs.Count, remapped.RemotePaths.Count);
        }

        return new SettingsImportPreview
        {
            ExportedAt = document.ExportedAt,
            SectionsInFile = document.IncludedSections(),
            SectionsToApply = sections,
            Connections = plan.Steps,
            Removals = plan.Removals,
            PreferenceChanges = prepared.Preferences?.Changes ?? [],
            Workspace = workspace,
            PinnedCommands = sections.PinnedCommands ? document.PinnedCommands!.Count : 0,
            ConnectionsWithPassword = CollectConnectionIds(prepared.Loaded.Secrets, ArchivedSecretKind.Password),
            ConnectionsWithSudoPassword = CollectConnectionIds(prepared.Loaded.Secrets, ArchivedSecretKind.SudoPassword),
            Warnings = warnings,
        };
    }

    private static HashSet<Guid> CollectConnectionIds(SecretsArchiveContent? secrets, ArchivedSecretKind kind)
    {
        var ids = new HashSet<Guid>();
        if (secrets is null)
        {
            return ids;
        }

        for (var i = 0; i < secrets.Secrets.Count; i++)
        {
            if (secrets.Secrets[i].Kind == kind)
            {
                ids.Add(secrets.Secrets[i].ConnectionId);
            }
        }

        return ids;
    }

    private static List<string> CollectWarnings(PreparedSettingsImport prepared)
    {
        var warnings = new List<string>();
        var secrets = prepared.Loaded.Secrets;
        var sections = prepared.Sections;
        if (secrets is not null && secrets.Secrets.Count > 0 && !sections.Connections)
        {
            warnings.Add("Saved passwords from the settings file are imported only together with server connections.");
        }

        var steps = prepared.Plan.Steps;
        for (var i = 0; i < steps.Count; i++)
        {
            var warning = DescribeStep(steps[i], secrets);
            if (warning is not null)
            {
                warnings.Add(warning);
            }
        }

        return warnings;
    }

    private static string? DescribeStep(ConnectionImportStep step, SecretsArchiveContent? secrets)
    {
        var hasArchivedPassword = secrets?.Find(step.Imported.Id, ArchivedSecretKind.Password) is not null;
        var keepsOwnVersion = step.Action == ConnectionImportAction.Conflict && step.Resolution == ConnectionConflictResolution.KeepExisting;
        if (keepsOwnVersion && hasArchivedPassword)
        {
            return $"'{step.Draft.Name}': you keep your version, so its password from the settings file is not imported.";
        }

        if (!step.WritesConnection || step.Draft.AuthMethod != ConnectionAuthMethod.Password || hasArchivedPassword)
        {
            return null;
        }

        var keepsSavedPassword = !step.IsNewConnection && step.Existing is not null && step.Existing.HasPassword;
        return keepsSavedPassword ? null : $"'{step.Draft.Name}' signs in with a password. Enter it in the server settings after the import.";
    }
}
