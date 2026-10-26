namespace DTech.Core.Docker;

public sealed record DockerContainer
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Image { get; init; }

    public required DockerContainerState State { get; init; }

    public required string Status { get; init; }

    public string? Health { get; init; }

    public string Ports { get; init; } = string.Empty;

    public string CreatedAt { get; init; } = string.Empty;
}
