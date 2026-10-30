namespace DTech.Core.AiBridge.Settings;

public sealed record AiBridgeSettings(bool Enabled, bool RequireApproval, string? TokenHash)
{
    public static AiBridgeSettings Default { get; } = new(false, true, null);

    public bool HasToken => !string.IsNullOrEmpty(TokenHash);
}
