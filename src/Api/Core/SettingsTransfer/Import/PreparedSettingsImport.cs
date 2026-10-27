namespace DTech.Core.SettingsTransfer.Import;

public sealed class PreparedSettingsImport(LoadedSettingsImport loaded, SettingsImportPlan plan, SettingsSections sections) : IDisposable
{
    public LoadedSettingsImport Loaded { get; } = loaded;

    public SettingsImportPlan Plan { get; } = plan;

    public SettingsSections Sections { get; } = sections;

    public PreferencesImportPlan? Preferences { get; init; }

    public void Dispose()
    {
        Loaded.Dispose();
    }
}
