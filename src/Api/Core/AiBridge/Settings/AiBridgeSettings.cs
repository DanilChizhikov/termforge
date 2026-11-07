namespace DTech.Core.AiBridge.Settings;

public sealed record AiBridgeSettings(bool Enabled, bool RequireApproval)
{
    public static AiBridgeSettings Default { get; } = new(false, true);
}
