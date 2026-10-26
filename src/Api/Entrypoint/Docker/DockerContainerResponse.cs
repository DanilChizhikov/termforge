using DTech.Core.Docker;

namespace DTech.Entrypoint.Docker;

internal sealed class DockerContainerResponse
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Image { get; init; }

    public required string State { get; init; }

    public required string Status { get; init; }

    public string? Health { get; init; }

    public required string Ports { get; init; }

    public required string CreatedAt { get; init; }

    public static DockerContainerResponse From(DockerContainer container)
    {
        return new DockerContainerResponse
        {
            Id = container.Id,
            Name = container.Name,
            Image = container.Image,
            State = container.State.ToString(),
            Status = container.Status,
            Health = container.Health,
            Ports = container.Ports,
            CreatedAt = container.CreatedAt,
        };
    }
}
