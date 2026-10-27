namespace DTech.Core.SettingsTransfer;

public readonly record struct SettingsSections(bool Connections, bool Preferences, bool Workspace, bool PinnedCommands)
{
    public bool Any => Connections || Preferences || Workspace || PinnedCommands;

    public SettingsSections Intersect(SettingsSections other)
    {
        return new SettingsSections(
            Connections && other.Connections,
            Preferences && other.Preferences,
            Workspace && other.Workspace,
            PinnedCommands && other.PinnedCommands);
    }
}
