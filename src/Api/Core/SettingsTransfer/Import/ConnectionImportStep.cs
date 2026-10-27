using DTech.Core.SavedConnections;
using DTech.Core.SettingsTransfer.Document;

namespace DTech.Core.SettingsTransfer.Import;

public sealed record ConnectionImportStep
{
    public required ExportedConnection Imported { get; init; }

    public required ConnectionImportAction Action { get; init; }

    public ConnectionConflictResolution Resolution { get; init; } = ConnectionConflictResolution.KeepExisting;

    public required Guid LocalId { get; init; }

    public SavedConnection? Existing { get; init; }

    public required SavedConnectionDraft Draft { get; init; }

    public IReadOnlyList<string> Changes { get; init; } = [];

    public bool IsNewConnection => Action == ConnectionImportAction.Add || IsResolvedAs(ConnectionConflictResolution.KeepBoth);

    public bool WritesConnection => Action == ConnectionImportAction.Update || IsNewConnection || IsResolvedAs(ConnectionConflictResolution.UseImported);

    public bool AppliesSecrets => WritesConnection || Action == ConnectionImportAction.Unchanged;

    private bool IsResolvedAs(ConnectionConflictResolution resolution)
    {
        return Action == ConnectionImportAction.Conflict && Resolution == resolution;
    }
}
