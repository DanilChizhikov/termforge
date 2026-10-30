using DTech.Core.AiBridge.Settings;

namespace DTech.Entrypoint.AiBridge;

internal sealed record AiBridgeSettingsResponse(bool Enabled, bool RequireApproval, bool HasToken, string McpUrl)
{
    public static AiBridgeSettingsResponse From(AiBridgeSettings settings, string mcpUrl)
    {
        return new AiBridgeSettingsResponse(settings.Enabled, settings.RequireApproval, settings.HasToken, mcpUrl);
    }
}
