using DTech.Core.SavedConnections;

namespace DTech.Core.SettingsTransfer.Import;

public sealed record SettingsImportPreview
{
    public required DateTime ExportedAt { get; init; }

    public required SettingsSections SectionsInFile { get; init; }

    public required SettingsSections SectionsToApply { get; init; }

    public bool HasSecretsArchive { get; init; }

    public required IReadOnlyList<ConnectionImportStep> Connections { get; init; }

    public required IReadOnlyList<SavedConnection> Removals { get; init; }

    public IReadOnlyList<string> PreferenceChanges { get; init; } = [];

    public WorkspaceImportSummary? Workspace { get; init; }

    public int PinnedCommands { get; init; }

    public bool SecretsSupplied { get; init; }

    public IReadOnlySet<Guid> ConnectionsWithPassword { get; init; } = new HashSet<Guid>();

    public IReadOnlySet<Guid> ConnectionsWithSudoPassword { get; init; } = new HashSet<Guid>();

    public required IReadOnlyList<string> Warnings { get; init; }
}
