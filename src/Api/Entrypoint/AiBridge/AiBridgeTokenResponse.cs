namespace DTech.Entrypoint.AiBridge;

internal sealed class AiBridgeTokenResponse
{
    public Guid Id { get; init; }

    public required string Name { get; init; }

    public DateTime CreatedAt { get; init; }

    public required string Token { get; init; }

    public required string McpUrl { get; init; }
}
