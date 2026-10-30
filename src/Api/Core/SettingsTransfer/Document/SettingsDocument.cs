using DTech.Core.Workspace;

namespace DTech.Core.SettingsTransfer.Document;

public sealed record SettingsDocument
{
    public string? Format { get; init; }

    public int SchemaVersion { get; init; }

    public Guid ExportId { get; init; }

    public DateTime ExportedAt { get; init; }

    public IReadOnlyList<ExportedConnection>? Connections { get; init; }

    public ExportedPreferences? Preferences { get; init; }

    public WorkspaceState? Workspace { get; init; }

    public IReadOnlyList<ExportedPinnedCommand>? PinnedCommands { get; init; }

    public SettingsSections IncludedSections()
    {
        return new SettingsSections(Connections is not null, Preferences is not null, Workspace is not null, PinnedCommands is not null);
    }
}
