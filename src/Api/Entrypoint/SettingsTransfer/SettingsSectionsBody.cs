using DTech.Core.SettingsTransfer;

namespace DTech.Entrypoint.SettingsTransfer;

public sealed class SettingsSectionsBody
{
    public bool Connections { get; set; }

    public bool Preferences { get; set; }

    public bool Workspace { get; set; }

    public bool PinnedCommands { get; set; }

    public static SettingsSectionsBody From(SettingsSections sections)
    {
        return new SettingsSectionsBody
        {
            Connections = sections.Connections,
            Preferences = sections.Preferences,
            Workspace = sections.Workspace,
            PinnedCommands = sections.PinnedCommands,
        };
    }

    public SettingsSections ToSections()
    {
        return new SettingsSections(Connections, Preferences, Workspace, PinnedCommands);
    }
}
