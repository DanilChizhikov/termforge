namespace DTech.Core.SettingsTransfer.Import;

public sealed record SettingsImportResult
{
    public int Added { get; init; }

    public int Updated { get; init; }

    public int Unchanged { get; init; }

    public int Kept { get; init; }

    public int Removed { get; init; }

    public int SecretsStored { get; init; }

    public bool PreferencesImported { get; init; }

    public bool WorkspaceImported { get; init; }

    public int PinnedCommandsAdded { get; init; }

    public int PinnedCommandsSkipped { get; init; }

    public required IReadOnlyList<string> Warnings { get; init; }
}
