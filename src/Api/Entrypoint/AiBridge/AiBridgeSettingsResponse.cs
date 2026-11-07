using DTech.Core.AiBridge.Settings;

namespace DTech.Entrypoint.AiBridge;

internal sealed record AiBridgeSettingsResponse(bool Enabled, bool RequireApproval, bool HasToken, string McpUrl)
{
    public static AiBridgeSettingsResponse From(AiBridgeSettings settings, string mcpUrl, bool hasToken)
    {
        return new AiBridgeSettingsResponse(settings.Enabled, settings.RequireApproval, hasToken, mcpUrl);
    }
}
