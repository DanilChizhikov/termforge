using DTech.Core.AiBridge.Settings;

namespace DTech.Entrypoint.AiBridge;

internal sealed record AiBridgeSettingsBody(bool Enabled, bool RequireApproval)
{
    public AiBridgePreferences ToPreferences()
    {
        return new AiBridgePreferences(Enabled, RequireApproval);
    }
}
