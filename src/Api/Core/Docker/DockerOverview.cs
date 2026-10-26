namespace DTech.Core.Docker;

public sealed record DockerOverview(Guid ConnectionId, string ServerName, DockerAvailability Availability, IReadOnlyList<DockerContainer> Containers);
