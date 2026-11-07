namespace DTech.Core.AiBridge.Settings;

public sealed class AiBridgeTokenRecord
{
    public Guid Id { get; init; }

    public required string Name { get; init; }

    public required string NormalizedName { get; init; }

    public required string TokenHash { get; init; }

    public DateTime CreatedAt { get; init; }
}
