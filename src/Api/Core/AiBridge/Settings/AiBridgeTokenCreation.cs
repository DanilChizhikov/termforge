namespace DTech.Core.AiBridge.Settings;

public sealed class AiBridgeTokenCreation
{
    public AiBridgeTokenInfo? Info { get; init; }

    public string? Token { get; init; }

    public AiBridgeTokenCreationError? Error { get; init; }
}
