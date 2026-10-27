namespace DTech.Core.SettingsTransfer.Import;

public sealed record SettingsImportOptions(ImportMode Mode, SettingsSections Sections, IReadOnlyDictionary<Guid, ConnectionConflictResolution> Resolutions)
{
    public bool ConfirmReplace { get; init; }

    public ConnectionConflictResolution? ResolutionFor(Guid importedId)
    {
        return Resolutions.TryGetValue(importedId, out var resolution) ? resolution : null;
    }
}
