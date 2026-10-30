using DTech.Core.AiBridge.Settings;

namespace DTech.Persistence.Settings;

internal sealed record AiBridgeSettingsDocument(bool Enabled, bool RequireApproval, string? TokenHash)
{
    public static AiBridgeSettingsDocument From(AiBridgeSettings settings)
    {
        return new AiBridgeSettingsDocument(settings.Enabled, settings.RequireApproval, settings.TokenHash);
    }

    public AiBridgeSettings ToSettings()
    {
        return new AiBridgeSettings(Enabled, RequireApproval, TokenHash);
    }
}
